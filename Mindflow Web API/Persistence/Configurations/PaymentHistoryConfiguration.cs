using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.Persistence.Configurations
{
    public class PaymentHistoryConfiguration : IEntityTypeConfiguration<PaymentHistory>
    {
        public void Configure(EntityTypeBuilder<PaymentHistory> builder)
        {
            // Define table name
            builder.ToTable("PaymentHistory");

            // Set primary key
            builder.HasKey(ph => ph.Id);

            // Configure properties
            builder.Property(ph => ph.UserId)
                   .IsRequired();

            builder.Property(ph => ph.PaymentCardId)
                   .IsRequired(false);

            builder.Property(ph => ph.SubscriptionPlanId)
                   .IsRequired(false);

            builder.Property(ph => ph.Amount)
                   .IsRequired()
                   .HasColumnType("decimal(18,2)");

            builder.Property(ph => ph.Currency)
                   .IsRequired()
                   .HasMaxLength(3);

            builder.Property(ph => ph.Description)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(ph => ph.Status)
                   .IsRequired()
                   .HasConversion<string>();

            builder.Property(ph => ph.TransactionId)
                   .HasMaxLength(100);

            builder.Property(ph => ph.PaymentMethod)
                   .HasMaxLength(50);

            builder.Property(ph => ph.FailureReason)
                   .HasMaxLength(500);

            builder.Property(ph => ph.TransactionDate)
                   .IsRequired();

            builder.Property(ph => ph.Created)
                   .IsRequired()
                   .ValueGeneratedOnAdd();

            builder.Property(ph => ph.LastModified)
                   .IsRequired()
                   .ValueGeneratedOnUpdate();

            // Configure relationships
            builder.HasOne(ph => ph.User)
                   .WithMany()
                   .HasForeignKey(ph => ph.UserId)
                   .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(ph => ph.PaymentCard)
                   .WithMany()
                   .HasForeignKey(ph => ph.PaymentCardId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(ph => ph.SubscriptionPlan)
                   .WithMany()
                   .HasForeignKey(ph => ph.SubscriptionPlanId)
                   .OnDelete(DeleteBehavior.SetNull);

            // Indexes for performance
            builder.HasIndex(ph => ph.UserId);
            builder.HasIndex(ph => ph.PaymentCardId);
            builder.HasIndex(ph => ph.SubscriptionPlanId);
            builder.HasIndex(ph => ph.Status);
            builder.HasIndex(ph => ph.TransactionDate);
            builder.HasIndex(ph => ph.TransactionId);
        }
    }
}
