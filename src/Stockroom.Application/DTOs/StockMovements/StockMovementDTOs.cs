using System.ComponentModel.DataAnnotations;
using Stockroom.Application.DTOs.Products;
using Stockroom.Domain.Entities;

namespace Stockroom.Application.DTOs.StockMovements;

public record StockAdjustmentRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be a positive integer greater than zero.")]
    public int Quantity { get; init; }

    [StringLength(250, ErrorMessage = "Note cannot exceed 250 characters.")]
    public string? Note { get; init; }
}

public record StockMovementResponse
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string MovementType { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public int QuantityBefore { get; init; }
    public int QuantityAfter { get; init; }
    public string? Note { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    public static StockMovementResponse FromEntity(StockMovement m) => new()
    {
        Id = m.Id,
        ProductId = m.ProductId,
        MovementType = m.MovementType.ToString(),
        Quantity = m.Quantity,
        QuantityBefore = m.QuantityBefore,
        QuantityAfter = m.QuantityAfter,
        Note = m.Note,
        CreatedAtUtc = m.CreatedAtUtc
    };
}

public record StockAdjustmentResult
{
    public ProductResponse Product { get; init; } = default!;
    public StockMovementResponse Movement { get; init; } = default!;
}
