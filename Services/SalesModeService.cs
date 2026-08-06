using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class SalesModeService(IWorkFlow workFlow) : WorkContainer<SalesMode>(workFlow), ISalesModeService
    {
    }
}