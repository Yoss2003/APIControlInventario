using System.Security.Cryptography;
using System.Text;

namespace InventoryAPI.Helpers
{
    public static class SecurityHelper
    {
        public static string GenerarHashSHA256(string textoPlano)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(textoPlano));
            StringBuilder builder = new();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("X2"));
            }
            return builder.ToString();
        }
    }
}