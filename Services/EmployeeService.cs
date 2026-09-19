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
            var employees = await _workFlow.Repository<Employee>()
                .GetAllWithIncludeAsync(e => e.User!);

            foreach (var emp in employees)
            {
                if (emp.User != null) emp.PictureUrl = emp.User.ProfilePictureUrl;
            }
            return employees;
        }

        public override async Task<bool> UpdateAsync(Employee employee)
        {
            var existingEmployee = await _workFlow.Repository<Employee>().GetByIdAsync(employee.Id);
            if (existingEmployee == null) return false;

            existingEmployee.FirstName = employee.FirstName;
            existingEmployee.LastName = employee.LastName;
            existingEmployee.DNI = employee.DNI;
            existingEmployee.JobPositionId = employee.JobPositionId;
            existingEmployee.AreaId = employee.AreaId;
            existingEmployee.CompanyId = employee.CompanyId;

            _workFlow.Repository<Employee>().Update(existingEmployee);
            var result = await _workFlow.CompleteAsync();
            return result > 0;
        }

        public override async Task<bool> DeleteAsync(int id, string deletedBy = "Sistema")
        {   
            var empMatch = await _workFlow.Repository<Employee>().GetAllWithIncludeAsync(e => e.User!);
            var empDb = empMatch.FirstOrDefault(e => e.Id == id);

            if (empDb == null) return false;

            // 1. Borrado lógico del Empleado
            empDb.IsActive = false;
            empDb.DeletionDate = DateTime.Now;
            empDb.DeletionUser = deletedBy;

            // 2. Borrado lógico en cascada del Usuario (si existe)
            if (empDb.User != null)
            {
                empDb.User.IsActive = false;
                empDb.User.DeletionDate = DateTime.Now;
                empDb.User.DeletionUser = deletedBy;
            }

            _workFlow.Repository<Employee>().Update(empDb);
            var result = await _workFlow.CompleteAsync();

            return result > 0;
        }
    }
}