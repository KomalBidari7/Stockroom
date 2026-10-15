namespace Stockroom.Application.DTOs.Inventory;

public record InventorySummaryResponse
{
    public int TotalProducts { get; init; }
    public int TotalUnitsInStock { get; init; }
    public int LowStockProductCount { get; init; }
    public decimal TotalInventoryValue { get; init; }
}
