using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockroom.Domain.Entities;

namespace Stockroom.Infrastructure.Data.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedOnAdd();

        builder.Property(m => m.ProductId)
            .IsRequired();

        builder.Property(m => m.MovementType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Quantity)
            .IsRequired();

        builder.Property(m => m.QuantityBefore)
            .IsRequired();

        builder.Property(m => m.QuantityAfter)
            .IsRequired();

        builder.Property(m => m.Note)
            .HasMaxLength(250);

        builder.Property(m => m.CreatedAtUtc)
            .IsRequired();

        // Index on ProductId and CreatedAtUtc descending for movement history queries
        builder.HasIndex(m => new { m.ProductId, m.CreatedAtUtc })
            .HasDatabaseName("IX_StockMovements_ProductId_CreatedAtUtc");

        builder.HasIndex(m => m.CreatedAtUtc)
            .HasDatabaseName("IX_StockMovements_CreatedAtUtc");
    }
}
