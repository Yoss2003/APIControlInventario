using InventoryAPI.Models;

namespace InventoryAPI.Services;

public static class SaleInstallmentCalculator
{
    // El proyecto opera en Perú: evitar adelantar el día por utilizar UTC.
    public static DateOnly BusinessToday()
    {
        var peru = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, peru));
    }

    public static bool TryBuild(decimal total, SaleInstallmentPlanRequest? request, DateOnly today,
        out List<SaleInstallment> installments, out string error)
    {
        installments = [];
        error = string.Empty;
        if (request == null)
        {
            error = "Configura la cantidad, frecuencia y cuota inicial de la venta a cuotas.";
            return false;
        }
        if (total <= 0 || decimal.Round(total, 2) != total ||
            request.DownPayment < 0 || decimal.Round(request.DownPayment, 2) != request.DownPayment ||
            request.DownPayment >= total)
        {
            error = "El total debe ser positivo y la cuota inicial debe ser menor al total (máximo dos decimales).";
            return false;
        }
        if (request.InstallmentCount is < 2 or > 120)
        {
            error = "El número de cuotas debe estar entre 2 y 120.";
            return false;
        }

        string frequency;
        if (request.Frequency?.Trim().Equals("Diario", StringComparison.OrdinalIgnoreCase) == true)
            frequency = "Diario";
        else if (request.Frequency?.Trim().Equals("Semanal", StringComparison.OrdinalIgnoreCase) == true)
            frequency = "Semanal";
        else if (request.Frequency?.Trim().Equals("Mensual", StringComparison.OrdinalIgnoreCase) == true)
            frequency = "Mensual";
        else
        {
            error = "Frecuencia inválida. Usa Diario, Semanal o Mensual.";
            return false;
        }

        var firstDueDate = request.FirstDueDate ?? Advance(today, frequency, 1);
        if (firstDueDate <= today)
        {
            error = "El primer vencimiento debe ser posterior a la fecha de venta.";
            return false;
        }

        var cents = decimal.ToInt64((total - request.DownPayment) * 100m);
        if (cents < request.InstallmentCount)
        {
            error = "El saldo financiado no alcanza para generar cuotas de al menos S/ 0.01.";
            return false;
        }
        long baseCents = cents / request.InstallmentCount;
        long remainder = cents % request.InstallmentCount;
        for (int i = 0; i < request.InstallmentCount; i++)
        {
            installments.Add(new SaleInstallment
            {
                InstallmentNumber = i + 1,
                InstallmentAmount = (baseCents + (i < remainder ? 1 : 0)) / 100m,
                PaidAmount = 0,
                DueDate = Advance(firstDueDate, frequency, i),
                Frequency = frequency
            });
        }
        return true;
    }

    private static DateOnly Advance(DateOnly date, string frequency, int intervals) => frequency switch
    {
        "Diario" => date.AddDays(intervals),
        "Semanal" => date.AddDays(7 * intervals),
        _ => date.AddMonths(intervals)
    };
}
