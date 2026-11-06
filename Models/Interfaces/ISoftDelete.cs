namespace InventoryAPI.Models.Interfaces
{
    public interface ISoftDelete
    {
        bool IsActive { get; set; }
        DateTime? DeletionDate { get; set; }
        string? DeletionUser { get; set; }
    }
}