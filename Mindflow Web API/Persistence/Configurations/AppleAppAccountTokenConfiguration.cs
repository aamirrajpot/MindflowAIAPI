using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.Persistence.Configurations
{
    public class AppleAppAccountTokenConfiguration : IEntityTypeConfiguration<AppleAppAccountToken>
    {
        public void Configure(EntityTypeBuilder<AppleAppAccountToken> builder)
        {
            builder.ToTable("AppleAppAccountTokens");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserId)
                   .IsRequired();

            builder.Property(x => x.AppAccountToken)
                   .IsRequired();

            builder.Property(x => x.IsActive)
                   .IsRequired();

            // Each token is unique
            builder.HasIndex(x => x.AppAccountToken)
                   .IsUnique();

            // Useful for querying by user
            builder.HasIndex(x => x.UserId);
        }
    }
}

