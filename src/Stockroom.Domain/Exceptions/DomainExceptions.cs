namespace Stockroom.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

public class InsufficientStockException : DomainException
{
    public int ProductId { get; }
    public int AvailableQuantity { get; }
    public int RequestedQuantity { get; }

    public InsufficientStockException(int productId, int availableQuantity, int requestedQuantity)
        : base($"Insufficient stock for product {productId}. Available: {availableQuantity}, Requested: {requestedQuantity}.")
    {
        ProductId = productId;
        AvailableQuantity = availableQuantity;
        RequestedQuantity = requestedQuantity;
    }
}

public class InvalidDomainDataException : DomainException
{
    public InvalidDomainDataException(string message) : base(message)
    {
    }
}

public class ProductConflictException : DomainException
{
    public ProductConflictException(string message) : base(message)
    {
    }
}

public class CannotDeleteProductException : DomainException
{
    public CannotDeleteProductException(string message) : base(message)
    {
    }
}

public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, object key)
        : base($"{entityName} with id '{key}' was not found.")
    {
    }
}
