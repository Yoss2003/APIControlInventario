using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class AccountReceivableService(IWorkFlow workFlow) : WorkContainer<AccountReceivable>(workFlow), IAccountReceivableService
    {
        public override async Task<IEnumerable<AccountReceivable>> GetAllByCompanyIdAsync(int companyId)
        {
            var cuentas = (await base.GetAllByCompanyIdAsync(companyId)).ToList();

            var perfil = await _workFlow.Repository<Profile>().GetByIdAsync(companyId);

            if (perfil == null || !perfil.ApplyLateFee)
            {
                return cuentas;
            }

            int diasGracia = perfil.GraceDays ?? 0;
            decimal porcentajeMoraDiaria = (decimal)(perfil.LateFeePercentage ?? 0f);
            DateTime hoy = DateTime.Today;

            foreach (var cuenta in cuentas.Where(c => c.Status == "Pending"))
            {
                if (DateTime.TryParse(cuenta.DueDate, out DateTime fechaVencimiento))
                {
                    if (fechaVencimiento.Date < hoy)
                    {
                        int diasAtrasoTotal = (hoy - fechaVencimiento.Date).Days;
                        int diasMoraAplicables = diasAtrasoTotal - diasGracia;

                        if (diasMoraAplicables > 0 && porcentajeMoraDiaria > 0)
                        {
                            decimal recargoMora = ((decimal)cuenta.InstallmentAmount * (porcentajeMoraDiaria / 100m)) * diasMoraAplicables;
                            cuenta.LateFeeAmount = (double)recargoMora;
                        }
                    }
                }
            }

            return cuentas;
        }
    }
}