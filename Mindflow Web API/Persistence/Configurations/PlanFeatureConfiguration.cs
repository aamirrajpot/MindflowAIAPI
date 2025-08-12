using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.Persistence.Configurations
{
    public class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
    {
        public void Configure(EntityTypeBuilder<PlanFeature> builder)
        {
            // Define table name
            builder.ToTable("PlanFeatures");

            // Set primary key
            builder.HasKey(pf => pf.Id);

            // Configure properties
            builder.Property(pf => pf.PlanId)
                   .IsRequired();

            builder.Property(pf => pf.FeatureId)
                   .IsRequired();

            builder.Property(pf => pf.IsIncluded)
                   .IsRequired();

            builder.Property(pf => pf.Limit)
                   .HasMaxLength(100);

            builder.Property(pf => pf.Created)
                   .IsRequired()
                   .ValueGeneratedOnAdd();

            builder.Property(pf => pf.LastModified)
                   .IsRequired()
                   .ValueGeneratedOnUpdate();

            // Configure relationships
            builder.HasOne(pf => pf.Plan)
                   .WithMany()
                   .HasForeignKey(pf => pf.PlanId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(pf => pf.Feature)
                   .WithMany()
                   .HasForeignKey(pf => pf.FeatureId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Composite unique index to prevent duplicate plan-feature combinations
            builder.HasIndex(pf => new { pf.PlanId, pf.FeatureId })
                   .IsUnique();

            // Indexes for performance
            builder.HasIndex(pf => pf.PlanId);
            builder.HasIndex(pf => pf.FeatureId);
            builder.HasIndex(pf => pf.IsIncluded);
        }
    }
}
