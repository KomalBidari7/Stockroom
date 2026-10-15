using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stockroom.Application.DTOs.Inventory;
using Stockroom.Application.DTOs.Products;
using Stockroom.Application.Interfaces;
using Stockroom.Domain.Common;

namespace Stockroom.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{Roles.Admin},{Roles.InventoryManager},{Roles.Viewer}")]
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
    /// Accessible to Viewer, InventoryManager, and Admin roles.
    /// </summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetLowStockProducts(
        CancellationToken cancellationToken)
    {
        var products = await _inventoryService.GetLowStockProductsAsync(cancellationToken);
        return Ok(products);
    }

    /// <summary>
    /// Computes high-level inventory metrics: product count, total units, low-stock count, and total inventory value.
    /// Note: Total inventory value represents current stock evaluated at recorded unit prices.
    /// Accessible to Viewer, InventoryManager, and Admin roles.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(InventorySummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InventorySummaryResponse>> GetSummary(
        CancellationToken cancellationToken)
    {
        var summary = await _inventoryService.GetInventorySummaryAsync(cancellationToken);
        return Ok(summary);
    }
}
