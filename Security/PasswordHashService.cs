using InventoryAPI.Helpers;
using System.Security.Cryptography;

namespace InventoryAPI.Security;

// Contraseñas nuevas con derivación lenta y sal aleatoria.
// Solo para compatibilidad, acepta hashes SHA256 existentes hasta migrarlos en el siguiente login.
public static class PasswordHashService
{
    private const int Iterations = 210_000;
    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored)) return false;
        if (!stored.StartsWith("PBKDF2$", StringComparison.Ordinal))
            return string.Equals(SecurityHelper.GenerarHashSHA256(password), stored, StringComparison.OrdinalIgnoreCase);
        try
        {
            var parts = stored.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out int iterations) || iterations < 100_000 || iterations > 1_000_000)
                return false;
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length < 16 || expected.Length != 32) return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }
}
