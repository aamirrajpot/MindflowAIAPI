using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.Persistence.Configurations
{
    public class PaymentCardConfiguration : IEntityTypeConfiguration<PaymentCard>
    {
        public void Configure(EntityTypeBuilder<PaymentCard> builder)
        {
            // Define table name
            builder.ToTable("PaymentCards");

            // Set primary key
            builder.HasKey(pc => pc.Id);

            // Configure properties
            builder.Property(pc => pc.UserId)
                   .IsRequired();

            builder.Property(pc => pc.CardNumber)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.Property(pc => pc.CardholderName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(pc => pc.ExpiryMonth)
                   .IsRequired()
                   .HasMaxLength(2);

            builder.Property(pc => pc.ExpiryYear)
                   .IsRequired()
                   .HasMaxLength(2);

            builder.Property(pc => pc.CardType)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.Property(pc => pc.IsDefault)
                   .IsRequired();

            builder.Property(pc => pc.IsActive)
                   .IsRequired();

            builder.Property(pc => pc.LastFourDigits)
                   .HasMaxLength(4);

            builder.Property(pc => pc.Created)
                   .IsRequired()
                   .ValueGeneratedOnAdd();

            builder.Property(pc => pc.LastModified)
                   .IsRequired()
                   .ValueGeneratedOnUpdate();

            // Configure relationships
            builder.HasOne(pc => pc.User)
                   .WithMany()
                   .HasForeignKey(pc => pc.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Indexes for performance
            builder.HasIndex(pc => pc.UserId);
            builder.HasIndex(pc => pc.IsDefault);
            builder.HasIndex(pc => pc.IsActive);
        }
    }
}
