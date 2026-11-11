using InventoryAPI.Models;

namespace InventoryAPI.Services;

public static class SalePaymentResolver
{
    public static bool TryResolve(string? category, string? wallet, PaymentType fallback, out PaymentType payment, out string error)
    {
        payment = fallback;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(category))
        {
            if (!string.IsNullOrWhiteSpace(wallet))
            {
                error = "Selecciona Billetera digital antes de indicar Yape, Plin o Bim.";
                return false;
            }
            if (Enum.IsDefined(fallback)) return true;
            error = "Método de pago desconocido.";
            return false;
        }

        var group = category.Trim();
        if (group.Equals("Billetera digital", StringComparison.OrdinalIgnoreCase) ||
            group.Equals("BilleteraDigital", StringComparison.OrdinalIgnoreCase) ||
            group.Equals("DigitalWallet", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(wallet) || !Enum.TryParse<PaymentType>(wallet.Trim(), true, out var parsed) ||
                parsed is not (PaymentType.Yape or PaymentType.Plin or PaymentType.Bim))
            {
                error = "Selecciona una billetera válida: Yape, Plin o Bim.";
                return false;
            }
            payment = parsed;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(wallet))
        {
            error = "La billetera solamente corresponde al método Billetera digital.";
            return false;
        }

        if (group.Equals("Venta a Cuotas", StringComparison.OrdinalIgnoreCase))
        {
            payment = PaymentType.Cuotas;
            return true;
        }

        if (Enum.TryParse<PaymentType>(group, true, out var value) && Enum.IsDefined(value))
        {
            payment = value;
            return true;
        }

        error = "Método de pago desconocido.";
        return false;
    }
}
