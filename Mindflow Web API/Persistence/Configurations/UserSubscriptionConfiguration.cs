using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.Persistence.Configurations
{
    public class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
    {
        public void Configure(EntityTypeBuilder<UserSubscription> builder)
        {
            // Define table name
            builder.ToTable("UserSubscriptions");

            // Set primary key
            builder.HasKey(us => us.Id);

            // Configure properties
            builder.Property(us => us.UserId)
                   .IsRequired();

            builder.Property(us => us.PlanId)
                   .IsRequired();

            builder.Property(us => us.StartDate)
                   .IsRequired();

            builder.Property(us => us.EndDate)
                   .IsRequired(false);

            builder.Property(us => us.Status)
                   .IsRequired()
                   .HasConversion<string>();

            builder.Property(us => us.Created)
                   .IsRequired()
                   .ValueGeneratedOnAdd();

            builder.Property(us => us.LastModified)
                   .IsRequired()
                   .ValueGeneratedOnUpdate();

            // Configure relationships
            builder.HasOne(us => us.User)
                   .WithMany()
                   .HasForeignKey(us => us.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(us => us.Plan)
                   .WithMany()
                   .HasForeignKey(us => us.PlanId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Indexes for performance
            builder.HasIndex(us => us.UserId);
            builder.HasIndex(us => us.PlanId);
            builder.HasIndex(us => us.Status);
            builder.HasIndex(us => us.StartDate);
            builder.HasIndex(us => us.EndDate);
        }
    }
}
