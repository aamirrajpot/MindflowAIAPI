using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mindflow_Web_API.Models;
using System.Text.Json;

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

            builder.Property(w => w.MoodLevel)
                   .IsRequired(false)  // Allow null/empty moodLevel
                   .HasMaxLength(50);

            builder.Property(w => w.CheckInDate)
                   .IsRequired();

            builder.Property(w => w.ReminderTime)
                   .HasMaxLength(20);

            builder.Property(w => w.AgeRange)
                   .HasMaxLength(20);

            builder.Property(w => w.WeekdayStartTime)
                   .HasMaxLength(10);

            builder.Property(w => w.WeekdayStartShift)
                   .HasMaxLength(2);

            builder.Property(w => w.WeekdayEndTime)
                   .HasMaxLength(10);

            builder.Property(w => w.WeekdayEndShift)
                   .HasMaxLength(2);

            builder.Property(w => w.WeekendStartTime)
                   .HasMaxLength(10);

            builder.Property(w => w.WeekendStartShift)
                   .HasMaxLength(2);

            builder.Property(w => w.WeekendEndTime)
                   .HasMaxLength(10);

            builder.Property(w => w.WeekendEndShift)
                   .HasMaxLength(2);

            // Configure UTC time fields (stored as DateTime in UTC)
            builder.Property(w => w.WeekdayStartTimeUtc)
                   .IsRequired(false);

            builder.Property(w => w.WeekdayEndTimeUtc)
                   .IsRequired(false);

            builder.Property(w => w.WeekendStartTimeUtc)
                   .IsRequired(false);

            builder.Property(w => w.WeekendEndTimeUtc)
                   .IsRequired(false);

            // Configure Questions dictionary as JSON column
            builder.Property(w => w.Questions)
                   .HasConversion(
                       v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                       v => string.IsNullOrWhiteSpace(v) 
                           ? new Dictionary<string, object>() 
                           : JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>())
                   .HasColumnType("TEXT");

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