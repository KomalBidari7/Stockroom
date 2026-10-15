using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Stockroom.Application.DTOs.Common;
using Stockroom.Application.DTOs.Inventory;
using Stockroom.Application.DTOs.Products;
using Stockroom.Application.DTOs.StockMovements;
using Xunit;

namespace Stockroom.Tests.Integration;

public class ApiEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostProduct_ValidPayload_Returns201AndLocationHeader()
    {
        var request = new CreateProductRequest
        {
            Name = "Standing Desk Pro",
            SKU = $"SDP-{Guid.NewGuid():N}"[..10],
            Description = "Motorized standing desk",
            UnitPrice = 599.99m,
            InitialQuantity = 10,
            LowStockThreshold = 2
        };

        var response = await _client.PostAsJsonAsync("/api/products", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var createdProduct = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(createdProduct);
        Assert.True(createdProduct.Id > 0);
        Assert.Equal(request.Name, createdProduct.Name);
        Assert.Equal(10, createdProduct.QuantityInStock);
        Assert.Contains($"/api/products/{createdProduct.Id}", response.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostProduct_InvalidData_Returns400BadRequestProblemDetails()
    {
        var invalidRequest = new
        {
            Name = "", // Empty name
            SKU = "VALID-SKU",
            UnitPrice = -5.00m // Negative price
        };

        var response = await _client.PostAsJsonAsync("/api/products", invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task PostProduct_DuplicateSku_Returns409ConflictProblemDetails()
    {
        var sku = $"DUP-{Guid.NewGuid():N}"[..10];
        var request1 = new CreateProductRequest
        {
            Name = "Original Item",
            SKU = sku,
            UnitPrice = 25m
        };

        var res1 = await _client.PostAsJsonAsync("/api/products", request1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        var request2 = new CreateProductRequest
        {
            Name = "Duplicate SKU Item",
            SKU = sku,
            UnitPrice = 35m
        };

        var res2 = await _client.PostAsJsonAsync("/api/products", request2);
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);

        var problem = await res2.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Contains(sku, problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetProductById_ExistingProduct_Returns200WithProduct()
    {
        var sku = $"GET-{Guid.NewGuid():N}"[..10];
        var createResponse = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Trackball Mouse",
            SKU = sku,
            UnitPrice = 65m,
            InitialQuantity = 4,
            LowStockThreshold = 1
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);

        var getResponse = await _client.GetAsync($"/api/products/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var product = await getResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);
        Assert.Equal(created.Id, product.Id);
        Assert.Equal("Trackball Mouse", product.Name);
    }

    [Fact]
    public async Task GetProductById_NonExistent_Returns404NotFoundProblemDetails()
    {
        var response = await _client.GetAsync("/api/products/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task StockInAndStockOut_E2EFlow_MaintainsAccurateAuditHistory()
    {
        var sku = $"FLOW-{Guid.NewGuid():N}"[..10];
        var createRes = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Webcam 4K",
            SKU = sku,
            UnitPrice = 129.99m,
            InitialQuantity = 5,
            LowStockThreshold = 2
        });
        var product = await createRes.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);

        // Stock-in 10
        var stockInRes = await _client.PostAsJsonAsync($"/api/products/{product.Id}/stock-in", new StockAdjustmentRequest
        {
            Quantity = 10,
            Note = "Added inventory"
        });
        Assert.Equal(HttpStatusCode.OK, stockInRes.StatusCode);
        var stockInResult = await stockInRes.Content.ReadFromJsonAsync<StockAdjustmentResult>();
        Assert.NotNull(stockInResult);
        Assert.Equal(15, stockInResult.Product.QuantityInStock);
        Assert.Equal(10, stockInResult.Movement.Quantity);

        // Stock-out 4
        var stockOutRes = await _client.PostAsJsonAsync($"/api/products/{product.Id}/stock-out", new StockAdjustmentRequest
        {
            Quantity = 4,
            Note = "Shipped order"
        });
        Assert.Equal(HttpStatusCode.OK, stockOutRes.StatusCode);
        var stockOutResult = await stockOutRes.Content.ReadFromJsonAsync<StockAdjustmentResult>();
        Assert.NotNull(stockOutResult);
        Assert.Equal(11, stockOutResult.Product.QuantityInStock);
        Assert.Equal(4, stockOutResult.Movement.Quantity);

        // Stock-out exceeding available balance (11) -> 409 Conflict
        var excessStockOutRes = await _client.PostAsJsonAsync($"/api/products/{product.Id}/stock-out", new StockAdjustmentRequest
        {
            Quantity = 20,
            Note = "Too many units"
        });
        Assert.Equal(HttpStatusCode.Conflict, excessStockOutRes.StatusCode);
        var conflictProblem = await excessStockOutRes.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(conflictProblem);
        Assert.Equal(409, conflictProblem.Status);

        // Fetch movements history
        var movementsRes = await _client.GetAsync($"/api/products/{product.Id}/movements");
        Assert.Equal(HttpStatusCode.OK, movementsRes.StatusCode);
        var movements = await movementsRes.Content.ReadFromJsonAsync<PagedResponse<StockMovementResponse>>();
        Assert.NotNull(movements);
        Assert.Equal(3, movements.TotalCount); // Initial (5), StockIn (10), StockOut (4)
        Assert.Equal("StockOut", movements.Items[0].MovementType); // Newest first
    }

    [Fact]
    public async Task DeleteProduct_WithExistingMovements_Returns409Conflict()
    {
        var sku = $"DEL-{Guid.NewGuid():N}"[..10];
        var createRes = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Preserved Item",
            SKU = sku,
            UnitPrice = 20m,
            InitialQuantity = 1
        });
        var product = await createRes.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);

        var delRes = await _client.DeleteAsync($"/api/products/{product.Id}");

        Assert.Equal(HttpStatusCode.Conflict, delRes.StatusCode);
        var problem = await delRes.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Contains("audit records exist", problem.Detail);
    }

    [Fact]
    public async Task InventoryReports_LowStockAndSummary_ReturnAccurateData()
    {
        var skuLow = $"LS-{Guid.NewGuid():N}"[..8];
        await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Low Item",
            SKU = skuLow,
            UnitPrice = 10m,
            InitialQuantity = 1,
            LowStockThreshold = 5
        });

        var lowStockRes = await _client.GetAsync("/api/inventory/low-stock");
        Assert.Equal(HttpStatusCode.OK, lowStockRes.StatusCode);
        var lowStockList = await lowStockRes.Content.ReadFromJsonAsync<List<ProductResponse>>();
        Assert.NotNull(lowStockList);
        Assert.Contains(lowStockList, p => p.SKU == skuLow.ToUpperInvariant());

        var summaryRes = await _client.GetAsync("/api/inventory/summary");
        Assert.Equal(HttpStatusCode.OK, summaryRes.StatusCode);
        var summary = await summaryRes.Content.ReadFromJsonAsync<InventorySummaryResponse>();
        Assert.NotNull(summary);
        Assert.True(summary.TotalProducts > 0);
        Assert.True(summary.TotalUnitsInStock > 0);
        Assert.True(summary.LowStockProductCount > 0);
        Assert.True(summary.TotalInventoryValue > 0);
    }

    [Fact]
    public async Task OpenApi_JsonEndpoint_Returns200WithApiDocument()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Stockroom", json, StringComparison.OrdinalIgnoreCase);
    }
}
