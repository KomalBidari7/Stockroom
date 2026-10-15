namespace Stockroom.Domain.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string InventoryManager = "InventoryManager";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyList<string> All = [Admin, InventoryManager, Viewer];

    public static bool IsValidRole(string role) =>
        All.Contains(role, StringComparer.OrdinalIgnoreCase);
}

public static class Policies
{
    public const string RequireAdmin = "RequireAdmin";
    public const string RequireInventoryManager = "RequireInventoryManager";
    public const string RequireViewer = "RequireViewer";
}
