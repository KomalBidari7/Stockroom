using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Stockroom.API.Middleware;
using Stockroom.Application;
using Stockroom.Infrastructure;
using Stockroom.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Configure lowercase routing
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

// Add Web API controllers
builder.Services.AddControllers();

// Configure ProblemDetails and Centralized Exception Handling
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Configure OpenAPI
builder.Services.AddOpenApi();

// Register Clean Architecture layers
builder.Services.AddApplicationServices();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=stockroom.db";
builder.Services.AddInfrastructureServices(connectionString);

var app = builder.Build();

// Centralized error handling pipeline
app.UseExceptionHandler();

// Enable OpenAPI and interactive API UI in development and testing
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Stockroom API Documentation")
            .WithTheme(ScalarTheme.Mars)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

// Automatic database migration on startup ensures zero-friction setup on a fresh clone
// without deleting or recreating the existing database.
if (!args.Contains("--no-migrate"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<StockroomDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
