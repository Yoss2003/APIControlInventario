using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class BrandService(IWorkFlow workFlow) : WorkContainer<Brand>(workFlow), IBrandService
    {
    }
}