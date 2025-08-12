using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.Persistence.Configurations
{
    public class SubscriptionFeatureConfiguration : IEntityTypeConfiguration<SubscriptionFeature>
    {
        public void Configure(EntityTypeBuilder<SubscriptionFeature> builder)
        {
            // Define table name
            builder.ToTable("SubscriptionFeatures");

            // Set primary key
            builder.HasKey(sf => sf.Id);

            // Configure properties
            builder.Property(sf => sf.Name)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(sf => sf.Description)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.Property(sf => sf.IsActive)
                   .IsRequired();

            builder.Property(sf => sf.SortOrder)
                   .IsRequired();

            builder.Property(sf => sf.Icon)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(sf => sf.Created)
                   .IsRequired()
                   .ValueGeneratedOnAdd();

            builder.Property(sf => sf.LastModified)
                   .IsRequired()
                   .ValueGeneratedOnUpdate();

            // Indexes for performance
            builder.HasIndex(sf => sf.Name);
            builder.HasIndex(sf => sf.IsActive);
            builder.HasIndex(sf => sf.SortOrder);
        }
    }
}
