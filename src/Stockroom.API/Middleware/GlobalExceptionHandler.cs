using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockroom.Domain.Exceptions;

namespace Stockroom.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = MapException(exception);

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled server error occurred while processing request to {Path}", httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning("Handled application error ({StatusCode} {Title}): {Message} for path {Path}",
                statusCode, title, exception.Message, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    private static (int StatusCode, string Title, string Detail) MapException(Exception exception)
    {
        return exception switch
        {
            EntityNotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                notFound.Message),

            InsufficientStockException insufficientStock => (
                StatusCodes.Status409Conflict,
                "Insufficient Stock",
                insufficientStock.Message),

            ProductConflictException conflict => (
                StatusCodes.Status409Conflict,
                "Resource Conflict",
                conflict.Message),

            CannotDeleteProductException deleteProhibited => (
                StatusCodes.Status409Conflict,
                "Operation Prohibited",
                deleteProhibited.Message),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Concurrency Conflict",
                "The requested resource was modified by a concurrent transaction. Please reload the latest data and retry."),

            AuthenticationFailedException authFailed => (
                StatusCodes.Status401Unauthorized,
                "Authentication Failed",
                authFailed.Message),

            IdentityValidationException identityVal => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                identityVal.Message),

            RoleOperationException roleOp => (
                StatusCodes.Status400BadRequest,
                "Role Operation Error",
                roleOp.Message),

            InvalidDomainDataException invalidData => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                invalidData.Message),

            ArgumentException argEx => (
                StatusCodes.Status400BadRequest,
                "Invalid Argument",
                argEx.Message),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred while processing your request.")
        };
    }
}
