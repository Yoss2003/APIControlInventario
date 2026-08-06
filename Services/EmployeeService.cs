using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class EmployeeService(IWorkFlow workFlow) : WorkContainer<Employee>(workFlow), IEmployeeService
    {
        public override async Task<IEnumerable<Employee>> GetAllAsync()
        {
            // Usamos el método Include que creamos para el repositorio genérico
            var employees = await _workFlow.Repository<Employee>()
                .GetAllWithIncludeAsync(e => e.User!);

            foreach (var emp in employees)
            {
                if (emp.User != null)
                {
                    emp.PictureUrl = emp.User.ProfilePictureUrl;
                }
            }

            return employees;
        }
    }
}