using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.Persistence.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            // Define table name
            builder.ToTable("Users");

            // Set primary key
            builder.HasKey(u => u.Id);

            // Configure properties
            builder.Property(u => u.UserName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(u => u.Email)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(u => u.EmailConfirmed)
                   .IsRequired();

            builder.Property(u => u.PasswordHash)
                   .IsRequired();

            builder.Property(u => u.SecurityStamp)
                   .IsRequired();

            builder.Property(u => u.IsActive)
                   .IsRequired();

            builder.Property(u => u.FirstName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(u => u.LastName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(u => u.DateOfBirth)
                   .IsRequired(false);

            builder.Property(u => u.Role)
                   .HasConversion<string>()
                   .HasMaxLength(20)
                   .IsRequired();

            builder.Property(u => u.StripeCustomerId)
                   .HasMaxLength(255)
                   .IsRequired(false);

            builder.Property(u => u.QuestionnaireFilled)
                   .IsRequired()
                   .HasDefaultValue(false);

            builder.Property(u => u.Created)
                   .IsRequired()
                   .ValueGeneratedOnAdd();

            builder.Property(u => u.LastModified)
                   .IsRequired()
                   .ValueGeneratedOnUpdate();

            // Indexes for performance
            builder.HasIndex(u => u.Email);
            builder.HasIndex(u => u.UserName);
            builder.HasIndex(u => u.Role);
        }
    }
} 