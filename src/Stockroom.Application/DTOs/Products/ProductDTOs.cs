using System.ComponentModel.DataAnnotations;
using Stockroom.Domain.Entities;

namespace Stockroom.Application.DTOs.Products;

public record CreateProductRequest
{
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(150, MinimumLength = 1, ErrorMessage = "Product name must be between 1 and 150 characters.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "SKU is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "SKU must be between 1 and 50 characters.")]
    public string SKU { get; init; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; init; }

    [Range(0, 10_000_000, ErrorMessage = "UnitPrice must not be negative.")]
    public decimal UnitPrice { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "InitialQuantity must not be negative.")]
    public int InitialQuantity { get; init; } = 0;

    [Range(0, int.MaxValue, ErrorMessage = "LowStockThreshold must not be negative.")]
    public int LowStockThreshold { get; init; } = 0;
}

public record UpdateProductRequest
{
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(150, MinimumLength = 1, ErrorMessage = "Product name must be between 1 and 150 characters.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "SKU is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "SKU must be between 1 and 50 characters.")]
    public string SKU { get; init; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; init; }

    [Range(0, 10_000_000, ErrorMessage = "UnitPrice must not be negative.")]
    public decimal UnitPrice { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "LowStockThreshold must not be negative.")]
    public int LowStockThreshold { get; init; }
}

public record ProductResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string SKU { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal UnitPrice { get; init; }
    public int QuantityInStock { get; init; }
    public int LowStockThreshold { get; init; }
    public bool IsLowStock { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }

    public static ProductResponse FromEntity(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        SKU = p.SKU,
        Description = p.Description,
        UnitPrice = p.UnitPrice,
        QuantityInStock = p.QuantityInStock,
        LowStockThreshold = p.LowStockThreshold,
        IsLowStock = p.LowStockThreshold > 0 && p.QuantityInStock <= p.LowStockThreshold,
        CreatedAtUtc = p.CreatedAtUtc,
        UpdatedAtUtc = p.UpdatedAtUtc
    };
}
