# Secrets Management Guide for Azure Web App

This guide explains how to securely manage API keys and secrets for the Mindflow Web API deployed on Azure Web App.

## 🔐 Azure Web App Setup

The application reads secrets from Azure Web App Application Settings, which are automatically available as environment variables.

## 📁 Azure Web App Configuration

### Setting Secrets in Azure Portal

1. **Go to Azure Portal**
   - Navigate to your Web App
   - Click **"Configuration"** in the left sidebar
   - Click **"Application settings"**

2. **Add Application Settings**
   Click **"+ New application setting"** for each secret:

   | **Name** | **Value** | **Description** |
   |----------|-----------|-----------------|
   | `Stripe__SecretKey` | `sk_live_your_production_key` | Your Stripe secret key |
   | `Stripe__PublishableKey` | `pk_live_your_production_key` | Your Stripe publishable key |
   | `Stripe__WebhookSecret` | `whsec_your_webhook_secret` | Your Stripe webhook secret |
   | `Jwt__Key` | `your_jwt_secret_key_here` | JWT signing key |
   | `Email__SmtpPassword` | `your_email_password` | SMTP password |

3. **Save Changes**
   - Click **"Save"** at the top
   - Your app will automatically restart

### Using Azure CLI (Alternative)

If you prefer command line:

```bash
az webapp config appsettings set --name "your-webapp-name" --resource-group "your-resource-group" --settings "Stripe__SecretKey=sk_live_your_production_key"
```

## 🚀 Azure Web App Deployment

Your application is configured to work seamlessly with Azure Web App Application Settings. The configuration system automatically reads from Azure's environment variables.

## 🔒 Security Best Practices

1. **Never commit secrets to source control**
2. **Use different keys for development and production**
3. **Rotate keys regularly**
4. **Use least privilege principle**
5. **Monitor access to secrets**

## 📋 Required Secrets

### Stripe Configuration
- `Stripe:SecretKey` - Your Stripe secret key
- `Stripe:PublishableKey` - Your Stripe publishable key
- `Stripe:WebhookSecret` - Your Stripe webhook endpoint secret

### Database
- `ConnectionStrings:DefaultConnection` - Database connection string

### JWT
- `Jwt:Key` - Secret key for JWT token signing
- `Jwt:Issuer` - JWT issuer
- `Jwt:Audience` - JWT audience

### Email
- `Email:SmtpPassword` - SMTP password for email sending

## 🛠️ Testing Configuration

To test if your configuration is working:

1. **Check if secrets are loaded:**
```bash
dotnet run --environment Development
```

2. **Verify Stripe connection:**
- Try creating a test customer
- Check webhook endpoint

3. **Test email sending:**
- Use the send-otp endpoint

## 🔧 Troubleshooting

### Common Issues

1. **"Stripe configuration is missing"**
   - Check if secrets are properly set
   - Verify environment variables are correct

2. **"Invalid API key"**
   - Ensure you're using the correct key (test vs live)
   - Check for extra spaces or characters

3. **"Webhook signature verification failed"**
   - Verify webhook secret is correct
   - Check if webhook URL is properly configured

### Debug Configuration

Add this to Program.cs temporarily for debugging:
```csharp
// Debug configuration (remove in production)
var stripeConfig = builder.Configuration.GetSection("Stripe");
Console.WriteLine($"Stripe Secret Key: {stripeConfig["SecretKey"]?.Substring(0, 7)}...");
Console.WriteLine($"Stripe Publishable Key: {stripeConfig["PublishableKey"]?.Substring(0, 7)}...");
```

## 📝 Environment-Specific Files

Create environment-specific configuration files:

- `appsettings.Development.json` - Development settings
- `appsettings.Production.json` - Production settings
- `appsettings.Staging.json` - Staging settings

These files should only contain non-sensitive configuration and should be committed to source control.

## 🔄 Key Rotation

When rotating keys:

1. **Update the secret in your chosen method**
2. **Restart the application**
3. **Update webhook endpoints in Stripe dashboard**
4. **Test all payment flows**

## 📞 Support

If you encounter issues with secret management:

1. Check the application logs
2. Verify configuration sources
3. Test with a simple configuration first
4. Contact the development team
