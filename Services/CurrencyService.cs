using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class CurrencyService(IWorkFlow workFlow) : WorkContainer<Currency>(workFlow), ICurrencyService
    {
    }
}