using Stockroom.Domain.Enums;
using Stockroom.Domain.Exceptions;

namespace Stockroom.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; private set; } = string.Empty;
    public string SKU { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int QuantityInStock { get; private set; }
    public int LowStockThreshold { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Concurrency token updated on each modification to prevent lost updates.
    /// </summary>
    public string ConcurrencyVersion { get; set; } = Guid.NewGuid().ToString("N");

    // Navigation property
    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();

    // Parameterless constructor for EF Core
    private Product() { }

    public Product(
        string name,
        string sku,
        string? description,
        decimal unitPrice,
        int initialQuantity,
        int lowStockThreshold,
        DateTime? createdAtUtc = null)
    {
        ValidateName(name);
        ValidateSku(sku);
        ValidateUnitPrice(unitPrice);
        ValidateQuantity(initialQuantity);
        ValidateLowStockThreshold(lowStockThreshold);

        Name = name.Trim();
        SKU = NormalizeSku(sku);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UnitPrice = unitPrice;
        QuantityInStock = initialQuantity;
        LowStockThreshold = lowStockThreshold;
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
        UpdatedAtUtc = null;
        ConcurrencyVersion = Guid.NewGuid().ToString("N");
    }

    public void UpdateDetails(
        string name,
        string sku,
        string? description,
        decimal unitPrice,
        int lowStockThreshold,
        DateTime? updatedAtUtc = null)
    {
        ValidateName(name);
        ValidateSku(sku);
        ValidateUnitPrice(unitPrice);
        ValidateLowStockThreshold(lowStockThreshold);

        Name = name.Trim();
        SKU = NormalizeSku(sku);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UnitPrice = unitPrice;
        LowStockThreshold = lowStockThreshold;
        UpdatedAtUtc = updatedAtUtc ?? DateTime.UtcNow;
        ConcurrencyVersion = Guid.NewGuid().ToString("N");
    }

    public StockMovement AdjustStockIn(int quantity, string? note = null, DateTime? timestampUtc = null)
    {
        if (quantity <= 0)
        {
            throw new InvalidDomainDataException("Stock-in quantity must be a positive integer greater than zero.");
        }

        var before = QuantityInStock;
        var after = checked(QuantityInStock + quantity);

        QuantityInStock = after;
        UpdatedAtUtc = timestampUtc ?? DateTime.UtcNow;
        ConcurrencyVersion = Guid.NewGuid().ToString("N");

        return new StockMovement(
            Id,
            StockMovementType.StockIn,
            quantity,
            before,
            after,
            note,
            UpdatedAtUtc.Value);
    }

    public StockMovement AdjustStockOut(int quantity, string? note = null, DateTime? timestampUtc = null)
    {
        if (quantity <= 0)
        {
            throw new InvalidDomainDataException("Stock-out quantity must be a positive integer greater than zero.");
        }

        if (QuantityInStock < quantity)
        {
            throw new InsufficientStockException(Id, QuantityInStock, quantity);
        }

        var before = QuantityInStock;
        var after = QuantityInStock - quantity;

        QuantityInStock = after;
        UpdatedAtUtc = timestampUtc ?? DateTime.UtcNow;
        ConcurrencyVersion = Guid.NewGuid().ToString("N");

        return new StockMovement(
            Id,
            StockMovementType.StockOut,
            quantity,
            before,
            after,
            note,
            UpdatedAtUtc.Value);
    }

    public static string NormalizeSku(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new InvalidDomainDataException("SKU is required.");
        }

        return sku.Trim().ToUpperInvariant();
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidDomainDataException("Product name is required.");
        }

        if (name.Trim().Length > 150)
        {
            throw new InvalidDomainDataException("Product name cannot exceed 150 characters.");
        }
    }

    private static void ValidateSku(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new InvalidDomainDataException("Product SKU is required.");
        }

        if (sku.Trim().Length > 50)
        {
            throw new InvalidDomainDataException("Product SKU cannot exceed 50 characters.");
        }
    }

    private static void ValidateUnitPrice(decimal unitPrice)
    {
        if (unitPrice < 0)
        {
            throw new InvalidDomainDataException("UnitPrice must not be negative.");
        }
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity < 0)
        {
            throw new InvalidDomainDataException("QuantityInStock must never be negative.");
        }
    }

    private static void ValidateLowStockThreshold(int threshold)
    {
        if (threshold < 0)
        {
            throw new InvalidDomainDataException("LowStockThreshold must not be negative.");
        }
    }
}
