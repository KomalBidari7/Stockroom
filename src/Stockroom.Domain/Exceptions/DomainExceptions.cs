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

public class AuthenticationFailedException : DomainException
{
    public AuthenticationFailedException(string message = "Invalid email or password.")
        : base(message)
    {
    }
}

public class IdentityValidationException : DomainException
{
    public IReadOnlyList<string> Errors { get; }

    public IdentityValidationException(IEnumerable<string> errors)
        : base(string.Join("; ", errors))
    {
        Errors = errors.ToList();
    }
}

public class RoleOperationException : DomainException
{
    public RoleOperationException(string message) : base(message)
    {
    }
}
