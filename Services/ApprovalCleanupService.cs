using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace InventoryAPI.Services
{
    public class ApprovalCleanupService(IServiceProvider serviceProvider) : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var limiteTiempo = DateTime.UtcNow.AddHours(-48);

                    var usuariosCaducados = await context.Users
                        .Include(u => u.Employee)
                        .Where(u => u.StatusId == 2 && u.CreatedAt != null && u.CreatedAt < limiteTiempo)
                        .ToListAsync(stoppingToken);

                    if (usuariosCaducados.Count != 0)
                    {
                        foreach (var user in usuariosCaducados)
                        {
                            user.IsActive = false;
                            user.StatusId = 3;

                            user.Employee?.StatusId = 3;
                        }

                        await context.SaveChangesAsync(stoppingToken);
                        Debug.WriteLine($"[CRON VIGILANTE]: Se suspendieron {usuariosCaducados.Count} empleados por exceder las 48 hrs sin aprobación.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CRON ERROR]: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}