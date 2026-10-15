using Stockroom.Domain.Entities;
using Stockroom.Domain.Enums;
using Stockroom.Domain.Exceptions;
using Xunit;

namespace Stockroom.Tests.Domain;

public class ProductTests
{
    [Fact]
    public void CreateProduct_WithValidData_InstantiatesSuccessfully()
    {
        var product = new Product("Mechanical Keyboard", "KEY-001", "RGB Gaming", 89.99m, 10, 5);

        Assert.Equal("Mechanical Keyboard", product.Name);
        Assert.Equal("KEY-001", product.SKU);
        Assert.Equal("RGB Gaming", product.Description);
        Assert.Equal(89.99m, product.UnitPrice);
        Assert.Equal(10, product.QuantityInStock);
        Assert.Equal(5, product.LowStockThreshold);
        Assert.True(product.CreatedAtUtc <= DateTime.UtcNow);
        Assert.Null(product.UpdatedAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(product.ConcurrencyVersion));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateProduct_WithInvalidName_ThrowsInvalidDomainDataException(string? invalidName)
    {
        Assert.Throws<InvalidDomainDataException>(() =>
            new Product(invalidName!, "KEY-001", null, 10m, 5, 2));
    }

    [Fact]
    public void CreateProduct_WithNameExceeding150Characters_ThrowsInvalidDomainDataException()
    {
        var longName = new string('A', 151);
        Assert.Throws<InvalidDomainDataException>(() =>
            new Product(longName, "KEY-001", null, 10m, 5, 2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateProduct_WithInvalidSku_ThrowsInvalidDomainDataException(string? invalidSku)
    {
        Assert.Throws<InvalidDomainDataException>(() =>
            new Product("Valid Name", invalidSku!, null, 10m, 5, 2));
    }

    [Fact]
    public void CreateProduct_WithNegativePrice_ThrowsInvalidDomainDataException()
    {
        Assert.Throws<InvalidDomainDataException>(() =>
            new Product("Valid Name", "SKU-01", null, -0.01m, 5, 2));
    }

    [Fact]
    public void CreateProduct_WithNegativeInitialQuantity_ThrowsInvalidDomainDataException()
    {
        Assert.Throws<InvalidDomainDataException>(() =>
            new Product("Valid Name", "SKU-01", null, 10m, -1, 2));
    }

    [Fact]
    public void CreateProduct_WithNegativeLowStockThreshold_ThrowsInvalidDomainDataException()
    {
        Assert.Throws<InvalidDomainDataException>(() =>
            new Product("Valid Name", "SKU-01", null, 10m, 5, -1));
    }

    [Fact]
    public void UpdateDetails_WithValidData_UpdatesPropertiesAndConcurrencyToken()
    {
        var product = new Product("Original", "SKU-OLD", "Old Desc", 50m, 10, 5);
        var oldVersion = product.ConcurrencyVersion;

        product.UpdateDetails("Updated Name", "sku-new", "New Desc", 75.50m, 8);

        Assert.Equal("Updated Name", product.Name);
        Assert.Equal("SKU-NEW", product.SKU);
        Assert.Equal("New Desc", product.Description);
        Assert.Equal(75.50m, product.UnitPrice);
        Assert.Equal(8, product.LowStockThreshold);
        Assert.NotNull(product.UpdatedAtUtc);
        Assert.NotEqual(oldVersion, product.ConcurrencyVersion);
    }

    [Fact]
    public void AdjustStockIn_WithPositiveQuantity_IncreasesBalanceAndReturnsMovement()
    {
        var product = new Product("Widget", "WDG-01", null, 20m, 15, 5);

        var movement = product.AdjustStockIn(10, "Received shipment");

        Assert.Equal(25, product.QuantityInStock);
        Assert.Equal(StockMovementType.StockIn, movement.MovementType);
        Assert.Equal(10, movement.Quantity);
        Assert.Equal(15, movement.QuantityBefore);
        Assert.Equal(25, movement.QuantityAfter);
        Assert.Equal("Received shipment", movement.Note);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AdjustStockIn_WithNonPositiveQuantity_ThrowsInvalidDomainDataException(int invalidQty)
    {
        var product = new Product("Widget", "WDG-01", null, 20m, 15, 5);

        Assert.Throws<InvalidDomainDataException>(() =>
            product.AdjustStockIn(invalidQty));
    }

    [Fact]
    public void AdjustStockOut_WithValidQuantity_DecreasesBalanceAndReturnsMovement()
    {
        var product = new Product("Widget", "WDG-01", null, 20m, 15, 5);

        var movement = product.AdjustStockOut(5, "Sold 5 units");

        Assert.Equal(10, product.QuantityInStock);
        Assert.Equal(StockMovementType.StockOut, movement.MovementType);
        Assert.Equal(5, movement.Quantity);
        Assert.Equal(15, movement.QuantityBefore);
        Assert.Equal(10, movement.QuantityAfter);
        Assert.Equal("Sold 5 units", movement.Note);
    }

    [Fact]
    public void AdjustStockOut_WhenInsufficientStock_ThrowsInsufficientStockException()
    {
        var product = new Product("Widget", "WDG-01", null, 20m, 5, 2);

        var ex = Assert.Throws<InsufficientStockException>(() =>
            product.AdjustStockOut(10));

        Assert.Equal(5, ex.AvailableQuantity);
        Assert.Equal(10, ex.RequestedQuantity);
        Assert.Equal(5, product.QuantityInStock); // Unchanged
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void AdjustStockOut_WithNonPositiveQuantity_ThrowsInvalidDomainDataException(int invalidQty)
    {
        var product = new Product("Widget", "WDG-01", null, 20m, 15, 5);

        Assert.Throws<InvalidDomainDataException>(() =>
            product.AdjustStockOut(invalidQty));
    }
}
