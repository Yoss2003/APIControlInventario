using System.Text.Json.Serialization;

namespace InventoryAPI.Models
{
    public class SaleDetail
    {
        public int Id { get; set; }

        // Relación hacia el ticket principal
        public int SaleId { get; set; }
        [JsonIgnore] // Evita ciclos infinitos al leer el JSON en la API
        public Sale? Sale { get; set; }

        // Relación hacia el producto que se está vendiendo
        public int ArticleId { get; set; }
        // public Product? Product { get; set; } // Descomenta esta línea si ya tienes tu modelo Product.cs creado

        public decimal Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal SubTotal { get; set; }
        public int CompanyId { get; set; }
    }
}