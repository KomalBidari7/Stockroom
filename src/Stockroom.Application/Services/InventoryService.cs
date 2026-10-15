using Stockroom.Application.DTOs.Common;
using Stockroom.Application.DTOs.Inventory;
using Stockroom.Application.DTOs.Products;
using Stockroom.Application.DTOs.StockMovements;
using Stockroom.Application.Interfaces;
using Stockroom.Domain.Exceptions;

namespace Stockroom.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IProductRepository _productRepository;
    private readonly IStockMovementRepository _movementRepository;
    private readonly IUnitOfWork _unitOfWork;

    public InventoryService(
        IProductRepository productRepository,
        IStockMovementRepository movementRepository,
        IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _movementRepository = movementRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<StockAdjustmentResult> StockInAsync(
        int productId,
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new InvalidDomainDataException("Stock-in quantity must be a positive integer greater than zero.");
        }

        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product == null)
        {
            throw new EntityNotFoundException("Product", productId);
        }

        var movement = product.AdjustStockIn(request.Quantity, request.Note);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _productRepository.UpdateAsync(product, cancellationToken);
            await _movementRepository.AddAsync(movement, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return new StockAdjustmentResult
        {
            Product = ProductResponse.FromEntity(product),
            Movement = StockMovementResponse.FromEntity(movement)
        };
    }

    public async Task<StockAdjustmentResult> StockOutAsync(
        int productId,
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new InvalidDomainDataException("Stock-out quantity must be a positive integer greater than zero.");
        }

        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product == null)
        {
            throw new EntityNotFoundException("Product", productId);
        }

        // Domain method validates quantity and throws InsufficientStockException if unavailable
        var movement = product.AdjustStockOut(request.Quantity, request.Note);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _productRepository.UpdateAsync(product, cancellationToken);
            await _movementRepository.AddAsync(movement, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return new StockAdjustmentResult
        {
            Product = ProductResponse.FromEntity(product),
            Movement = StockMovementResponse.FromEntity(movement)
        };
    }

    public async Task<PagedResponse<StockMovementResponse>> GetProductMovementsAsync(
        int productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product == null)
        {
            throw new EntityNotFoundException("Product", productId);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await _movementRepository.GetPagedByProductIdAsync(
            productId,
            page,
            pageSize,
            cancellationToken);

        var responseItems = items.Select(StockMovementResponse.FromEntity).ToList();

        return new PagedResponse<StockMovementResponse>(responseItems, page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<ProductResponse>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetLowStockProductsAsync(cancellationToken);
        return products.Select(ProductResponse.FromEntity).ToList();
    }

    public async Task<InventorySummaryResponse> GetInventorySummaryAsync(CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetAllForSummaryAsync(cancellationToken);

        var totalProducts = products.Count;
        var totalUnitsInStock = products.Sum(p => (long)p.QuantityInStock);
        var lowStockCount = products.Count(p => p.LowStockThreshold > 0 && p.QuantityInStock <= p.LowStockThreshold);

        // Safe decimal arithmetic to avoid floating point or SQLite precision translation quirks
        var totalInventoryValue = products.Sum(p => p.UnitPrice * p.QuantityInStock);

        return new InventorySummaryResponse
        {
            TotalProducts = totalProducts,
            TotalUnitsInStock = (int)totalUnitsInStock,
            LowStockProductCount = lowStockCount,
            TotalInventoryValue = totalInventoryValue
        };
    }
}
