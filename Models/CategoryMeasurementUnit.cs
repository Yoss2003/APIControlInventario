namespace InventoryAPI.Models
{
    public class CategoryMeasurementUnit
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public int MeasurementUnitId { get; set; }
        public int CompanyId { get; set; }
    }
}
