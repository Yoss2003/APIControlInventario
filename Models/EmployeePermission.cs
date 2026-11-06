using System;
using System.Collections.Generic;
using System.Text;

namespace InventoryAPI.Models
{
    public class EmployeePermission
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public bool CanApplyDiscounts { get; set; } = false;
        public bool CanViewPurchaseCosts { get; set; } = false;
        public bool CanCancelSales { get; set; } = false;
        public bool CanEditProducts { get; set; } = false;
        public bool CanViewProfits { get; set; } = false;
        public int CompanyId { get; set; }
    }
}
