using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.Persistence.Configurations
{
    public class WellnessCheckInConfiguration : IEntityTypeConfiguration<WellnessCheckIn>
    {
        public void Configure(EntityTypeBuilder<WellnessCheckIn> builder)
        {
            // Define table name
            builder.ToTable("WellnessCheckIns");

            // Set primary key
            builder.HasKey(w => w.Id);

            // Configure properties
            builder.Property(w => w.UserId)
                   .IsRequired();

            builder.Property(w => w.StressLevel)
                   .IsRequired();

            builder.Property(w => w.MoodLevel)
                   .IsRequired()
                   .HasMaxLength(10);

            builder.Property(w => w.EnergyLevel)
                   .IsRequired()
                   .HasMaxLength(20);

            builder.Property(w => w.SpiritualWellness)
                   .IsRequired();

            builder.Property(w => w.CheckInDate)
                   .IsRequired();

            builder.Property(w => w.Created)
                   .IsRequired()
                   .ValueGeneratedOnAdd();

            builder.Property(w => w.LastModified)
                   .IsRequired()
                   .ValueGeneratedOnUpdate();

            // Configure foreign key relationship
            builder.HasOne<User>()
                   .WithMany()
                   .HasForeignKey(w => w.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Indexes for performance
            builder.HasIndex(w => w.UserId);
            builder.HasIndex(w => w.CheckInDate);
        }
    }
} 