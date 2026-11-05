using ControlInventario.Shared.Models;
using ControlInventario.Shared.Models.DTO;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface IEmployeeService : IWorkContainer<Employee>
    {
        Task<(bool Success, string Message)> UpdateEmployeeWithUserAsync(int id, EmployeeUpdateDTO dto);
    }
}