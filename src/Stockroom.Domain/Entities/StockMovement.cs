using Stockroom.Domain.Enums;
using Stockroom.Domain.Exceptions;

namespace Stockroom.Domain.Entities;

public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; private set; }
    public StockMovementType MovementType { get; private set; }
    public int Quantity { get; private set; }
    public int QuantityBefore { get; private set; }
    public int QuantityAfter { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // Navigation property
    public Product? Product { get; private set; }

    // Parameterless constructor for EF Core
    private StockMovement() { }

    public StockMovement(
        int productId,
        StockMovementType movementType,
        int quantity,
        int quantityBefore,
        int quantityAfter,
        string? note,
        DateTime? createdAtUtc = null)
    {
        if (quantity <= 0)
        {
            throw new InvalidDomainDataException("Movement quantity must be greater than zero.");
        }

        if (quantityBefore < 0)
        {
            throw new InvalidDomainDataException("QuantityBefore must be non-negative.");
        }

        if (quantityAfter < 0)
        {
            throw new InvalidDomainDataException("QuantityAfter must be non-negative.");
        }

        if (movementType == StockMovementType.StockIn && quantityAfter != checked(quantityBefore + quantity))
        {
            throw new InvalidDomainDataException("QuantityAfter must equal QuantityBefore + Quantity for StockIn.");
        }

        if (movementType == StockMovementType.StockOut && quantityAfter != quantityBefore - quantity)
        {
            throw new InvalidDomainDataException("QuantityAfter must equal QuantityBefore - Quantity for StockOut.");
        }

        ProductId = productId;
        MovementType = movementType;
        Quantity = quantity;
        QuantityBefore = quantityBefore;
        QuantityAfter = quantityAfter;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
    }
}
