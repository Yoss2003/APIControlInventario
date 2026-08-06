using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface IInstallmentPaymentService : IWorkContainer<InstallmentPayment>
    {
    }
}