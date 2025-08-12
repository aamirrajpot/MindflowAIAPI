using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.Persistence.Configurations
{
    public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
    {
        public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
        {
            // Define table name
            builder.ToTable("SubscriptionPlans");

            // Set primary key
            builder.HasKey(sp => sp.Id);

            // Configure properties
            builder.Property(sp => sp.Name)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(sp => sp.Description)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.Property(sp => sp.Price)
                   .IsRequired()
                   .HasColumnType("decimal(18,2)");

            builder.Property(sp => sp.BillingCycle)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.Property(sp => sp.IsActive)
                   .IsRequired();

            builder.Property(sp => sp.SortOrder)
                   .IsRequired();

            builder.Property(sp => sp.OriginalPrice)
                   .HasMaxLength(50);

            builder.Property(sp => sp.IsPopular)
                   .IsRequired();

            builder.Property(sp => sp.Created)
                   .IsRequired()
                   .ValueGeneratedOnAdd();

            builder.Property(sp => sp.LastModified)
                   .IsRequired()
                   .ValueGeneratedOnUpdate();

            // Indexes for performance
            builder.HasIndex(sp => sp.Name);
            builder.HasIndex(sp => sp.IsActive);
            builder.HasIndex(sp => sp.SortOrder);
        }
    }
}
