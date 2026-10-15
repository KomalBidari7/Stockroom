using Stockroom.Domain.Entities;
using Stockroom.Domain.Enums;
using Stockroom.Domain.Exceptions;
using Xunit;

namespace Stockroom.Tests.Domain;

public class StockMovementTests
{
    [Fact]
    public void CreateStockMovement_StockIn_ValidBalances_Succeeds()
    {
        var movement = new StockMovement(1, StockMovementType.StockIn, 5, 10, 15, "Restock");

        Assert.Equal(1, movement.ProductId);
        Assert.Equal(StockMovementType.StockIn, movement.MovementType);
        Assert.Equal(5, movement.Quantity);
        Assert.Equal(10, movement.QuantityBefore);
        Assert.Equal(15, movement.QuantityAfter);
        Assert.Equal("Restock", movement.Note);
        Assert.True(movement.CreatedAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void CreateStockMovement_StockOut_ValidBalances_Succeeds()
    {
        var movement = new StockMovement(1, StockMovementType.StockOut, 4, 10, 6, "Order dispatch");

        Assert.Equal(1, movement.ProductId);
        Assert.Equal(StockMovementType.StockOut, movement.MovementType);
        Assert.Equal(4, movement.Quantity);
        Assert.Equal(10, movement.QuantityBefore);
        Assert.Equal(6, movement.QuantityAfter);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateStockMovement_InvalidQuantity_ThrowsInvalidDomainDataException(int invalidQty)
    {
        Assert.Throws<InvalidDomainDataException>(() =>
            new StockMovement(1, StockMovementType.StockIn, invalidQty, 10, 10 + invalidQty, null));
    }

    [Fact]
    public void CreateStockMovement_NegativeBalances_ThrowsInvalidDomainDataException()
    {
        Assert.Throws<InvalidDomainDataException>(() =>
            new StockMovement(1, StockMovementType.StockOut, 5, -1, -6, null));
    }

    [Fact]
    public void CreateStockMovement_StockIn_MismatchedBalanceMath_ThrowsInvalidDomainDataException()
    {
        Assert.Throws<InvalidDomainDataException>(() =>
            new StockMovement(1, StockMovementType.StockIn, 5, 10, 20, null)); // 10 + 5 != 20
    }

    [Fact]
    public void CreateStockMovement_StockOut_MismatchedBalanceMath_ThrowsInvalidDomainDataException()
    {
        Assert.Throws<InvalidDomainDataException>(() =>
            new StockMovement(1, StockMovementType.StockOut, 5, 10, 3, null)); // 10 - 5 != 3
    }
}
