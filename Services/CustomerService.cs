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
                string token = "sk_13723.0lArrSFwUExN4vbBK34oN97ryg6uZhQw";

                client.DefaultRequestHeaders.Clear();

                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                string urlExterna = $"https://api.decolecta.com/v1/reniec/dni?numero={dni}";
                var response = await client.GetAsync(urlExterna);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    return (true, jsonContent);
                }

                var errorReal = await response.Content.ReadAsStringAsync();

                return (false, $"Error Decolecta HTTP {(int)response.StatusCode}: {errorReal}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}