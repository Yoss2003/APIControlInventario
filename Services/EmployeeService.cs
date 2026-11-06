using InventoryAPI.Models;
using InventoryAPI.Models.DTO;
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

            var activeEmployees = employees.Where(e => e.IsActive).ToList();

            foreach (var emp in activeEmployees)
            {
                if (emp.User != null)
                {
                    emp.PictureUrl = emp.User.ProfilePictureUrl;
                }
            }
            return activeEmployees;
        }

        public override async Task<IEnumerable<Employee>> GetAllByCompanyIdAsync(int companyId)
        {
            var employees = await _workFlow.Repository<Employee>()
                .GetAllWithIncludeAsync(e => e.User!);

            var activeEmployees = employees.Where(e => e.CompanyId == companyId && e.IsActive).ToList();

            foreach (var emp in activeEmployees)
            {
                if (emp.User != null)
                {
                    emp.PictureUrl = emp.User.ProfilePictureUrl;
                }
            }
            return activeEmployees;
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

        public async Task<(bool Success, string Message)> UpdateEmployeeWithUserAsync(int id, EmployeeUpdateDTO dto)
        {
            try
            {
                var employeeDb = await _workFlow.Repository<Employee>().GetByIdAsync(id);
                if (employeeDb == null) return (false, "Empleado no encontrado.");

                employeeDb.FirstName = dto.Employee.FirstName;
                employeeDb.LastName = dto.Employee.LastName;
                employeeDb.DNI = dto.Employee.DNI;
                employeeDb.JobPositionId = dto.Employee.JobPositionId;
                employeeDb.AreaId = dto.Employee.AreaId;
                employeeDb.CompanyId = dto.Employee.CompanyId;

                if (employeeDb.UserId.HasValue && employeeDb.UserId > 0 && !string.IsNullOrWhiteSpace(dto.UpdatedEmail))
                {
                    var userDb = await _workFlow.Repository<User>().GetByIdAsync(employeeDb.UserId.Value);
                    if (userDb != null)
                    {
                        userDb.Email = dto.UpdatedEmail;
                        _workFlow.Repository<User>().Update(userDb);
                    }
                }

                _workFlow.Repository<Employee>().Update(employeeDb);

                await _workFlow.CompleteAsync();

                return (true, "Empleado y usuario actualizados correctamente.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR TRANSACCIÓN EMPLEADO]: {ex.Message}");
                return (false, "Ocurrió un error interno al actualizar los registros.");
            }
        }

        public override async Task<bool> DeleteAsync(int id, string deletedBy = "Sistema")
        {
            var empMatch = await _workFlow.Repository<Employee>().GetAllWithIncludeAsync(e => e.User!);
            var existingEmployee = empMatch.FirstOrDefault(e => e.Id == id);

            if (existingEmployee == null) return false;

            existingEmployee.IsActive = false;

            existingEmployee.User?.IsActive = false;

            _workFlow.Repository<Employee>().Update(existingEmployee);
            var result = await _workFlow.CompleteAsync();
            return result > 0;
        }
    }
}