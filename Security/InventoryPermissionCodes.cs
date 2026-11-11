namespace InventoryAPI.Security;

/// <summary>Permisos de alcance empresarial. Se combinan con pertenencia a empresa y asignaciones.</summary>
public static class InventoryPermissionCodes
{
    public const string ViewMaster = "VIEW_MASTER_INVENTORY";
    public const string ManageMaster = "MANAGE_MASTER_CATALOG";
    public const string CreateInventory = "CREATE_INVENTORIES";
    public const string ManageInventoryAccess = "MANAGE_INVENTORY_ACCESS";
    public const string ManageUsers = "MANAGE_USERS";
    public const string ViewUsers = "VIEW_USERS";

    public static readonly IReadOnlyDictionary<string, string> Definitions =
        new Dictionary<string, string>
        {
            [ViewMaster] = "Consultar el catálogo maestro de la empresa",
            [ManageMaster] = "Administrar catálogos maestros de la empresa",
            [CreateInventory] = "Crear y administrar inventarios de trabajo propios",
            [ManageInventoryAccess] = "Compartir y administrar accesos a inventarios",
            [ManageUsers] = "Crear y editar usuarios de la empresa",
            [ViewUsers] = "Consultar usuarios y roles de la empresa"
        };
}
