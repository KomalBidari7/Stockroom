using Microsoft.EntityFrameworkCore;
using Stockroom.Application.DTOs.Products;
using Stockroom.Application.DTOs.StockMovements;
using Stockroom.Application.Services;
using Stockroom.Domain.Exceptions;
using Stockroom.Infrastructure.Data;
using Stockroom.Infrastructure.Repositories;
using Stockroom.Tests.TestHelpers;
using Xunit;

namespace Stockroom.Tests.Application;

public class InventoryServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly ProductRepository _productRepo;
    private readonly StockMovementRepository _movementRepo;
    private readonly UnitOfWork _unitOfWork;
    private readonly ProductService _productService;
    private readonly InventoryService _inventoryService;

    public InventoryServiceTests()
    {
        _db = new SqliteTestDatabase();
        _productRepo = new ProductRepository(_db.Context);
        _movementRepo = new StockMovementRepository(_db.Context);
        _unitOfWork = new UnitOfWork(_db.Context);
        _productService = new ProductService(_productRepo, _movementRepo, _unitOfWork);
        _inventoryService = new InventoryService(_productRepo, _movementRepo, _unitOfWork);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task StockIn_UpdatesProductBalanceAndPersistsMovementRecord()
    {
        var product = await _productService.CreateProductAsync(new CreateProductRequest
        {
            Name = "USB Cable",
            SKU = "USB-01",
            UnitPrice = 5.99m,
            InitialQuantity = 10,
            LowStockThreshold = 2
        });

        var result = await _inventoryService.StockInAsync(product.Id, new StockAdjustmentRequest
        {
            Quantity = 25,
            Note = "Shipment #1042"
        });

        Assert.Equal(35, result.Product.QuantityInStock);
        Assert.Equal(product.Id, result.Movement.ProductId);
        Assert.Equal("StockIn", result.Movement.MovementType);
        Assert.Equal(25, result.Movement.Quantity);
        Assert.Equal(10, result.Movement.QuantityBefore);
        Assert.Equal(35, result.Movement.QuantityAfter);
        Assert.Equal("Shipment #1042", result.Movement.Note);

        // Verify persisted state in database
        var movements = await _db.Context.StockMovements.Where(m => m.ProductId == product.Id).ToListAsync();
        Assert.Equal(2, movements.Count); // 1 initial + 1 stock-in
    }

    [Fact]
    public async Task StockOut_UpdatesProductBalanceAndPersistsMovementRecord()
    {
        var product = await _productService.CreateProductAsync(new CreateProductRequest
        {
            Name = "USB Cable",
            SKU = "USB-01",
            UnitPrice = 5.99m,
            InitialQuantity = 20,
            LowStockThreshold = 2
        });

        var result = await _inventoryService.StockOutAsync(product.Id, new StockAdjustmentRequest
        {
            Quantity = 7,
            Note = "Sales dispatch"
        });

        Assert.Equal(13, result.Product.QuantityInStock);
        Assert.Equal("StockOut", result.Movement.MovementType);
        Assert.Equal(7, result.Movement.Quantity);
        Assert.Equal(20, result.Movement.QuantityBefore);
        Assert.Equal(13, result.Movement.QuantityAfter);
        Assert.Equal("Sales dispatch", result.Movement.Note);
    }

    [Fact]
    public async Task StockOut_WhenInsufficientStock_ThrowsInsufficientStockExceptionAndLeavesBalanceUnchanged()
    {
        var product = await _productService.CreateProductAsync(new CreateProductRequest
        {
            Name = "Laptop",
            SKU = "LAP-01",
            UnitPrice = 1200m,
            InitialQuantity = 3,
            LowStockThreshold = 1
        });

        var initialMovementsCount = await _db.Context.StockMovements.CountAsync(m => m.ProductId == product.Id);

        var ex = await Assert.ThrowsAsync<InsufficientStockException>(() =>
            _inventoryService.StockOutAsync(product.Id, new StockAdjustmentRequest { Quantity = 5 }));

        Assert.Equal(3, ex.AvailableQuantity);
        Assert.Equal(5, ex.RequestedQuantity);

        // Verify product balance remains untouched
        var freshProduct = await _productService.GetProductByIdAsync(product.Id);
        Assert.NotNull(freshProduct);
        Assert.Equal(3, freshProduct.QuantityInStock);

        // Verify no additional movement records were created
        var afterMovementsCount = await _db.Context.StockMovements.CountAsync(m => m.ProductId == product.Id);
        Assert.Equal(initialMovementsCount, afterMovementsCount);
    }

    [Fact]
    public async Task StockOperation_MissingProduct_ThrowsEntityNotFoundException()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _inventoryService.StockInAsync(9999, new StockAdjustmentRequest { Quantity = 10 }));

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _inventoryService.StockOutAsync(9999, new StockAdjustmentRequest { Quantity = 10 }));
    }

    [Fact]
    public async Task GetProductMovements_ReturnsNewestFirstAndPaginated()
    {
        var product = await _productService.CreateProductAsync(new CreateProductRequest
        {
            Name = "Wireless Mouse",
            SKU = "MOU-01",
            UnitPrice = 25m,
            InitialQuantity = 5
        });

        await _inventoryService.StockInAsync(product.Id, new StockAdjustmentRequest { Quantity = 10, Note = "First" });
        await _inventoryService.StockOutAsync(product.Id, new StockAdjustmentRequest { Quantity = 3, Note = "Second" });

        var paged = await _inventoryService.GetProductMovementsAsync(product.Id, page: 1, pageSize: 2);

        Assert.Equal(2, paged.Items.Count);
        Assert.Equal(3, paged.TotalCount); // Initial, StockIn, StockOut
        Assert.Equal(2, paged.TotalPages);

        // Verify ordering: newest operation first ("Second")
        Assert.Equal("Second", paged.Items[0].Note);
        Assert.Equal("StockOut", paged.Items[0].MovementType);
    }

    [Fact]
    public async Task GetProductMovements_MissingProduct_ThrowsEntityNotFoundException()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _inventoryService.GetProductMovementsAsync(8888, 1, 10));
    }

    [Fact]
    public async Task LowStockReporting_ExcludesZeroThresholdProductsAndMatchesLowStockOnly()
    {
        // 1. Stock <= Threshold: Low stock
        await _productService.CreateProductAsync(new CreateProductRequest
        {
            Name = "Item Low",
            SKU = "LOW-01",
            UnitPrice = 10m,
            InitialQuantity = 2,
            LowStockThreshold = 5
        });

        // 2. Stock > Threshold: Normal stock
        await _productService.CreateProductAsync(new CreateProductRequest
        {
            Name = "Item Normal",
            SKU = "NRM-01",
            UnitPrice = 10m,
            InitialQuantity = 20,
            LowStockThreshold = 5
        });

        // 3. Stock = 0, but LowStockThreshold = 0: Excluded from low stock alerts by explicit rule
        await _productService.CreateProductAsync(new CreateProductRequest
        {
            Name = "Item Zero Threshold",
            SKU = "ZRO-01",
            UnitPrice = 10m,
            InitialQuantity = 0,
            LowStockThreshold = 0
        });

        var lowStockList = await _inventoryService.GetLowStockProductsAsync();

        Assert.Single(lowStockList);
        Assert.Equal("LOW-01", lowStockList[0].SKU);
    }

    [Fact]
    public async Task InventorySummary_CalculatesAccurateTotalsAndDecimalValuation()
    {
        // Product 1: 10 * 15.50 = 155.00, Low: 10 <= 15 -> true
        await _productService.CreateProductAsync(new CreateProductRequest
        {
            Name = "P1",
            SKU = "SKU-1",
            UnitPrice = 15.50m,
            InitialQuantity = 10,
            LowStockThreshold = 15
        });

        // Product 2: 5 * 100.25 = 501.25, Low: 5 <= 2 -> false
        await _productService.CreateProductAsync(new CreateProductRequest
        {
            Name = "P2",
            SKU = "SKU-2",
            UnitPrice = 100.25m,
            InitialQuantity = 5,
            LowStockThreshold = 2
        });

        // Product 3: 0 * 50.00 = 0.00, Threshold = 0 -> not low stock
        await _productService.CreateProductAsync(new CreateProductRequest
        {
            Name = "P3",
            SKU = "SKU-3",
            UnitPrice = 50.00m,
            InitialQuantity = 0,
            LowStockThreshold = 0
        });

        var summary = await _inventoryService.GetInventorySummaryAsync();

        Assert.Equal(3, summary.TotalProducts);
        Assert.Equal(15, summary.TotalUnitsInStock);
        Assert.Equal(1, summary.LowStockProductCount);
        Assert.Equal(656.25m, summary.TotalInventoryValue); // 155.00 + 501.25
    }
}
