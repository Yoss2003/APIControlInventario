using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Services
{
    public class ApprovalCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public ApprovalCleanupService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var limiteTiempo = DateTime.Now.AddHours(-48);

                        var usuariosCaducados = await context.Users
                            .Include(u => u.Employee)
                            .Where(u => u.StatusId == 2 && u.CreatedAt != null && u.CreatedAt < limiteTiempo)
                            .ToListAsync(stoppingToken);

                        if (usuariosCaducados.Any())
                        {
                            foreach (var user in usuariosCaducados)
                            {
                                user.IsActive = false;
                                user.StatusId = 3;

                                if (user.Employee != null)
                                {
                                    user.Employee.StatusId = 3;
                                }
                            }

                            await context.SaveChangesAsync(stoppingToken);
                            System.Diagnostics.Debug.WriteLine($"[CRON VIGILANTE]: Se suspendieron {usuariosCaducados.Count} empleados por exceder las 48 hrs sin aprobación.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CRON ERROR]: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}