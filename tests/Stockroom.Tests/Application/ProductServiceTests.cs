using Microsoft.EntityFrameworkCore;
using Stockroom.Application.DTOs.Products;
using Stockroom.Application.Services;
using Stockroom.Domain.Entities;
using Stockroom.Domain.Exceptions;
using Stockroom.Infrastructure.Data;
using Stockroom.Infrastructure.Repositories;
using Stockroom.Tests.TestHelpers;
using Xunit;

namespace Stockroom.Tests.Application;

public class ProductServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly ProductRepository _productRepo;
    private readonly StockMovementRepository _movementRepo;
    private readonly UnitOfWork _unitOfWork;
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _db = new SqliteTestDatabase();
        _productRepo = new ProductRepository(_db.Context);
        _movementRepo = new StockMovementRepository(_db.Context);
        _unitOfWork = new UnitOfWork(_db.Context);
        _service = new ProductService(_productRepo, _movementRepo, _unitOfWork);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task CreateProduct_WithZeroInitialQuantity_CreatesProductWithoutMovements()
    {
        var request = new CreateProductRequest
        {
            Name = "Ergonomic Chair",
            SKU = "CHR-001",
            Description = "Office chair",
            UnitPrice = 249.99m,
            InitialQuantity = 0,
            LowStockThreshold = 5
        };

        var response = await _service.CreateProductAsync(request);

        Assert.True(response.Id > 0);
        Assert.Equal("Ergonomic Chair", response.Name);
        Assert.Equal("CHR-001", response.SKU);
        Assert.Equal(0, response.QuantityInStock);

        var movements = await _db.Context.StockMovements.Where(m => m.ProductId == response.Id).ToListAsync();
        Assert.Empty(movements);
    }

    [Fact]
    public async Task CreateProduct_WithPositiveInitialQuantity_CreatesProductAndStockInMovementAtomically()
    {
        var request = new CreateProductRequest
        {
            Name = "Standing Desk",
            SKU = "DSK-100",
            Description = "Adjustable desk",
            UnitPrice = 499.00m,
            InitialQuantity = 15,
            LowStockThreshold = 3
        };

        var response = await _service.CreateProductAsync(request);

        Assert.True(response.Id > 0);
        Assert.Equal(15, response.QuantityInStock);

        var movements = await _db.Context.StockMovements.Where(m => m.ProductId == response.Id).ToListAsync();
        Assert.Single(movements);
        var movement = movements.First();
        Assert.Equal(Stockroom.Domain.Enums.StockMovementType.StockIn, movement.MovementType);
        Assert.Equal(15, movement.Quantity);
        Assert.Equal(0, movement.QuantityBefore);
        Assert.Equal(15, movement.QuantityAfter);
        Assert.Contains("Initial stock receipt", movement.Note);
    }

    [Fact]
    public async Task CreateProduct_DuplicateSku_ThrowsProductConflictException()
    {
        var request1 = new CreateProductRequest
        {
            Name = "Item One",
            SKU = "UNIQUE-SKU",
            UnitPrice = 10m
        };
        await _service.CreateProductAsync(request1);

        var request2 = new CreateProductRequest
        {
            Name = "Item Two",
            SKU = "unique-sku", // Case-insensitive duplicate check
            UnitPrice = 20m
        };

        await Assert.ThrowsAsync<ProductConflictException>(() =>
            _service.CreateProductAsync(request2));
    }

    [Fact]
    public async Task UpdateProduct_MetadataChanges_DoesNotAlterStockQuantity()
    {
        var createResponse = await _service.CreateProductAsync(new CreateProductRequest
        {
            Name = "Monitor",
            SKU = "MON-01",
            UnitPrice = 300m,
            InitialQuantity = 20,
            LowStockThreshold = 5
        });

        var updateRequest = new UpdateProductRequest
        {
            Name = "Curved Monitor 34-inch",
            SKU = "MON-01-CURVED",
            Description = "Ultra-wide",
            UnitPrice = 450m,
            LowStockThreshold = 10
        };

        var updated = await _service.UpdateProductAsync(createResponse.Id, updateRequest);

        Assert.Equal("Curved Monitor 34-inch", updated.Name);
        Assert.Equal("MON-01-CURVED", updated.SKU);
        Assert.Equal("Ultra-wide", updated.Description);
        Assert.Equal(450m, updated.UnitPrice);
        Assert.Equal(10, updated.LowStockThreshold);
        Assert.Equal(20, updated.QuantityInStock); // Quantity remains intact
    }

    [Fact]
    public async Task UpdateProduct_DuplicateSkuOnAnotherProduct_ThrowsProductConflictException()
    {
        await _service.CreateProductAsync(new CreateProductRequest { Name = "P1", SKU = "SKU-A", UnitPrice = 10m });
        var p2 = await _service.CreateProductAsync(new CreateProductRequest { Name = "P2", SKU = "SKU-B", UnitPrice = 20m });

        var updateRequest = new UpdateProductRequest
        {
            Name = "P2 New",
            SKU = "SKU-A", // Conflicts with P1
            UnitPrice = 25m,
            LowStockThreshold = 2
        };

        await Assert.ThrowsAsync<ProductConflictException>(() =>
            _service.UpdateProductAsync(p2.Id, updateRequest));
    }

    [Fact]
    public async Task DeleteProduct_WithNoMovementsAndZeroStock_DeletesSuccessfully()
    {
        var product = await _service.CreateProductAsync(new CreateProductRequest
        {
            Name = "Draft Product",
            SKU = "DFT-01",
            UnitPrice = 5m,
            InitialQuantity = 0
        });

        await _service.DeleteProductAsync(product.Id);

        var retrieved = await _service.GetProductByIdAsync(product.Id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task DeleteProduct_WithStockHistory_ThrowsCannotDeleteProductException()
    {
        var product = await _service.CreateProductAsync(new CreateProductRequest
        {
            Name = "Historical Product",
            SKU = "HIST-01",
            UnitPrice = 10m,
            InitialQuantity = 5
        });

        var ex = await Assert.ThrowsAsync<CannotDeleteProductException>(() =>
            _service.DeleteProductAsync(product.Id));

        Assert.Contains("audit records exist", ex.Message);

        // Verify product still exists in database
        var stillExists = await _service.GetProductByIdAsync(product.Id);
        Assert.NotNull(stillExists);
    }

    [Fact]
    public async Task DeleteProduct_WithPositiveStockAndNoMovements_ThrowsCannotDeleteProductException()
    {
        // Directly seed a product with positive stock but no movements
        var prod = new Product("Seed", "SEED-99", null, 10m, 5, 2);
        await _db.Context.Products.AddAsync(prod);
        await _db.Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<CannotDeleteProductException>(() =>
            _service.DeleteProductAsync(prod.Id));

        Assert.Contains("active balance of 5 units", ex.Message);
    }

    [Fact]
    public async Task GetProducts_FilteringAndPagination_WorksCorrectly()
    {
        await _service.CreateProductAsync(new CreateProductRequest { Name = "Apple iPhone", SKU = "APL-IPH", UnitPrice = 999m, InitialQuantity = 10, LowStockThreshold = 15 });
        await _service.CreateProductAsync(new CreateProductRequest { Name = "Apple iPad", SKU = "APL-IPD", UnitPrice = 699m, InitialQuantity = 2, LowStockThreshold = 5 });
        await _service.CreateProductAsync(new CreateProductRequest { Name = "Google Pixel", SKU = "GGL-PIX", UnitPrice = 799m, InitialQuantity = 20, LowStockThreshold = 5 });

        // Search for "Apple"
        var appleResults = await _service.GetProductsAsync(1, 10, search: "Apple", lowStockOnly: null);
        Assert.Equal(2, appleResults.TotalCount);

        // Filter lowStockOnly (iPhone: 10 <= 15 -> true, iPad: 2 <= 5 -> true, Pixel: 20 <= 5 -> false)
        var lowStockResults = await _service.GetProductsAsync(1, 10, search: null, lowStockOnly: true);
        Assert.Equal(2, lowStockResults.TotalCount);

        // Pagination test (page 1, pageSize 2 of 3 total items)
        var pagedResults = await _service.GetProductsAsync(1, 2, search: null, lowStockOnly: null);
        Assert.Equal(2, pagedResults.Items.Count);
        Assert.Equal(3, pagedResults.TotalCount);
        Assert.Equal(2, pagedResults.TotalPages);
    }

    [Fact]
    public async Task GetProductById_NonExistent_ReturnsNull()
    {
        var product = await _service.GetProductByIdAsync(9999);
        Assert.Null(product);
    }

    [Fact]
    public async Task UpdateProduct_ConcurrentModification_ThrowsDbUpdateConcurrencyException()
    {
        var product = await _service.CreateProductAsync(new CreateProductRequest
        {
            Name = "Original Item",
            SKU = "CONCUR-01",
            UnitPrice = 50m,
            InitialQuantity = 10
        });

        // Load the product in context 1
        var trackedProduct = await _db.Context.Products.FirstAsync(p => p.Id == product.Id);

        // Simulate another client modifying the product in the database directly
        await _db.Context.Database.ExecuteSqlRawAsync(
            "UPDATE Products SET ConcurrencyVersion = {0} WHERE Id = {1}",
            Guid.NewGuid().ToString("N"),
            product.Id);

        // Attempting to update the stale tracked product should detect the concurrency token mismatch
        trackedProduct.UpdateDetails("Changed Name", "CONCUR-01", null, 60m, 5);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            _db.Context.SaveChangesAsync());
    }
}
