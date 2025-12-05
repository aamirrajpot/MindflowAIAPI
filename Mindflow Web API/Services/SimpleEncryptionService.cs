using System.Security.Cryptography;
using System.Text;

namespace Mindflow_Web_API.Services
{
    public interface ISimpleEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }

    /// <summary>
    /// Simple AES encryption service for protecting OAuth tokens at rest.
    /// Uses a 32-byte key from configuration (EncryptionKey).
    /// </summary>
    public class SimpleEncryptionService : ISimpleEncryptionService
    {
        private readonly byte[] _key;

        public SimpleEncryptionService(IConfiguration configuration)
        {
            var jwtSection = configuration.GetSection("Jwt");
            var keyString = jwtSection["Key"];
            if (string.IsNullOrWhiteSpace(keyString) || keyString.Length < 32)
            {
                throw new InvalidOperationException("EncryptionKey must be configured and at least 32 characters long.");
            }

            _key = Encoding.UTF8.GetBytes(keyString[..32]);
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Prepend IV to cipher text
            var result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _key;

            var iv = new byte[aes.BlockSize / 8];
            var cipherBytes = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipherBytes, 0, cipherBytes.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}


