using Microsoft.AspNetCore.Mvc;
using Stockroom.Application.DTOs.Inventory;
using Stockroom.Application.DTOs.Products;
using Stockroom.Application.Interfaces;

namespace Stockroom.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// Retrieves all products whose current stock level is less than or equal to their low-stock threshold.
    /// Products with a low-stock threshold of 0 are excluded from low-stock alerts.
    /// </summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetLowStockProducts(
        CancellationToken cancellationToken)
    {
        var products = await _inventoryService.GetLowStockProductsAsync(cancellationToken);
        return Ok(products);
    }

    /// <summary>
    /// Computes high-level inventory metrics: product count, total units, low-stock count, and total inventory value.
    /// Note: Total inventory value represents current stock evaluated at recorded unit prices.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(InventorySummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InventorySummaryResponse>> GetSummary(
        CancellationToken cancellationToken)
    {
        var summary = await _inventoryService.GetInventorySummaryAsync(cancellationToken);
        return Ok(summary);
    }
}
