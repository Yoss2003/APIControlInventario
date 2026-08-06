using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class CustomerService(IWorkFlow workFlow) : WorkContainer<Customer>(workFlow), ICustomerService
    {
        public async Task<(bool IsSuccess, string DataOrError)> ConsultarDniExternoAsync(string dni)
        {
            if (string.IsNullOrWhiteSpace(dni) || dni.Length != 8)
            {
                return (false, "El DNI debe tener exactamente 8 dígitos.");
            }

            try
            {
                using var client = new HttpClient();
                string urlExterna = $"https://api.apis.net.pe/v1/dni?numero={dni}";
                var response = await client.GetAsync(urlExterna);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    return (true, jsonContent);
                }

                return (false, "El DNI no fue localizado en la base de datos pública.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}