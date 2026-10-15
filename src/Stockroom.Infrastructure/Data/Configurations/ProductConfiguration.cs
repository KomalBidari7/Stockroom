using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockroom.Domain.Entities;

namespace Stockroom.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.SKU)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.Property(p => p.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.QuantityInStock)
            .IsRequired();

        builder.Property(p => p.LowStockThreshold)
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc);

        builder.Property(p => p.ConcurrencyVersion)
            .IsRequired()
            .HasMaxLength(64)
            .IsConcurrencyToken();

        // Unique index on SKU
        builder.HasIndex(p => p.SKU)
            .IsUnique()
            .HasDatabaseName("IX_Products_SKU");

        // Indexes for fast searching and filtering
        builder.HasIndex(p => p.Name)
            .HasDatabaseName("IX_Products_Name");

        builder.HasIndex(p => p.QuantityInStock)
            .HasDatabaseName("IX_Products_QuantityInStock");

        builder.HasIndex(p => p.LowStockThreshold)
            .HasDatabaseName("IX_Products_LowStockThreshold");

        // Prevent cascade deletion so historical audit movements are never accidentally deleted
        builder.HasMany(p => p.Movements)
            .WithOne(m => m.Product)
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
