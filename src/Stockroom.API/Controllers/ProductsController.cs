using Microsoft.AspNetCore.Mvc;
using Stockroom.Application.DTOs.Common;
using Stockroom.Application.DTOs.Products;
using Stockroom.Application.DTOs.StockMovements;
using Stockroom.Application.Interfaces;

namespace Stockroom.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IInventoryService _inventoryService;

    public ProductsController(
        IProductService productService,
        IInventoryService inventoryService)
    {
        _productService = productService;
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// Creates a new product. If initialQuantity > 0, an initial StockIn movement is atomically recorded.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productService.CreateProductAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    /// <summary>
    /// Retrieves a paginated list of products with optional search and low-stock filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ProductResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] bool? lowStockOnly = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Pagination",
                Detail = "Page number must be greater than or equal to 1.",
                Instance = HttpContext.Request.Path
            });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Pagination",
                Detail = "Page size must be between 1 and 100.",
                Instance = HttpContext.Request.Path
            });
        }

        var result = await _productService.GetProductsAsync(page, pageSize, search, lowStockOnly, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a specific product by its ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var product = await _productService.GetProductByIdAsync(id, cancellationToken);
        if (product == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Product Not Found",
                Detail = $"Product with ID {id} was not found.",
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(product);
    }

    /// <summary>
    /// Updates product metadata (name, SKU, description, unit price, low-stock threshold).
    /// Does not alter quantity in stock.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Update(
        int id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _productService.UpdateProductAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a product only if it has zero stock balance and no historical stock movement audit records.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        await _productService.DeleteProductAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Adjusts stock upward (StockIn) and atomically records an immutable movement audit log.
    /// </summary>
    [HttpPost("{id:int}/stock-in")]
    [ProducesResponseType(typeof(StockAdjustmentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockAdjustmentResult>> StockIn(
        int id,
        [FromBody] StockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.StockInAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Adjusts stock downward (StockOut) and atomically records an immutable movement audit log.
    /// </summary>
    [HttpPost("{id:int}/stock-out")]
    [ProducesResponseType(typeof(StockAdjustmentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockAdjustmentResult>> StockOut(
        int id,
        [FromBody] StockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.StockOutAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves paginated stock movement audit history for the specified product, newest first.
    /// </summary>
    [HttpGet("{id:int}/movements")]
    [ProducesResponseType(typeof(PagedResponse<StockMovementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<StockMovementResponse>>> GetMovements(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Pagination",
                Detail = "Page number must be greater than or equal to 1.",
                Instance = HttpContext.Request.Path
            });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Pagination",
                Detail = "Page size must be between 1 and 100.",
                Instance = HttpContext.Request.Path
            });
        }

        var result = await _inventoryService.GetProductMovementsAsync(id, page, pageSize, cancellationToken);
        return Ok(result);
    }
}
