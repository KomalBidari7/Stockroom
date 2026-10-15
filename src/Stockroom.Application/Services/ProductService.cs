using Stockroom.Application.DTOs.Common;
using Stockroom.Application.DTOs.Products;
using Stockroom.Application.Interfaces;
using Stockroom.Domain.Entities;
using Stockroom.Domain.Exceptions;

namespace Stockroom.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IStockMovementRepository _movementRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(
        IProductRepository productRepository,
        IStockMovementRepository movementRepository,
        IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _movementRepository = movementRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductResponse> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedSku = Product.NormalizeSku(request.SKU);

        if (await _productRepository.ExistsBySkuAsync(normalizedSku, cancellationToken: cancellationToken))
        {
            throw new ProductConflictException($"A product with SKU '{normalizedSku}' already exists.");
        }

        // We initialize with 0, and if initialQuantity > 0, we apply AdjustStockIn atomically.
        // This ensures the stock movement audit record matches the starting balance.
        var product = new Product(
            request.Name,
            normalizedSku,
            request.Description,
            request.UnitPrice,
            0,
            request.LowStockThreshold);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _productRepository.AddAsync(product, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (request.InitialQuantity > 0)
            {
                var initialMovement = product.AdjustStockIn(
                    request.InitialQuantity,
                    "Initial stock receipt upon product creation");

                await _movementRepository.AddAsync(initialMovement, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }, cancellationToken);

        return ProductResponse.FromEntity(product);
    }

    public async Task<ProductResponse?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        return product == null ? null : ProductResponse.FromEntity(product);
    }

    public async Task<PagedResponse<ProductResponse>> GetProductsAsync(
        int page,
        int pageSize,
        string? search,
        bool? lowStockOnly,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await _productRepository.GetPagedAsync(
            page,
            pageSize,
            search,
            lowStockOnly,
            cancellationToken);

        var responseItems = items.Select(ProductResponse.FromEntity).ToList();

        return new PagedResponse<ProductResponse>(responseItems, page, pageSize, totalCount);
    }

    public async Task<ProductResponse> UpdateProductAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product == null)
        {
            throw new EntityNotFoundException("Product", id);
        }

        var normalizedSku = Product.NormalizeSku(request.SKU);

        if (await _productRepository.ExistsBySkuAsync(normalizedSku, excludeProductId: id, cancellationToken: cancellationToken))
        {
            throw new ProductConflictException($"A product with SKU '{normalizedSku}' already exists.");
        }

        product.UpdateDetails(
            request.Name,
            normalizedSku,
            request.Description,
            request.UnitPrice,
            request.LowStockThreshold);

        await _productRepository.UpdateAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ProductResponse.FromEntity(product);
    }

    public async Task DeleteProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product == null)
        {
            throw new EntityNotFoundException("Product", id);
        }

        var hasMovements = await _movementRepository.HasMovementsAsync(id, cancellationToken);
        if (hasMovements)
        {
            throw new CannotDeleteProductException(
                $"Cannot delete product '{product.Name}' (ID: {id}, SKU: {product.SKU}) because historical stock movement audit records exist. Deleting this product would destroy inventory audit integrity.");
        }

        if (product.QuantityInStock > 0)
        {
            throw new CannotDeleteProductException(
                $"Cannot delete product '{product.Name}' (ID: {id}, SKU: {product.SKU}) because it has an active balance of {product.QuantityInStock} units in stock. Balance must be 0 prior to deletion.");
        }

        await _productRepository.DeleteAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
