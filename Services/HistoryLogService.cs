using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class HistoryLogService(IWorkFlow workFlow) : WorkContainer<HistoryLog>(workFlow), IHistoryLogService
    {
    }
}