using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InventoryAPI.Services;

/// <summary>
/// Comparación de nombres en un catálogo de una sola empresa.
/// Nunca altera el nombre original que ve el usuario.
/// </summary>
public static class CatalogNames
{
    public static string Key(string? name)
    {
        var text = Regex.Replace((name ?? string.Empty).Trim(), @"\s+", " ").Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark &&
                category != UnicodeCategory.SpacingCombiningMark &&
                category != UnicodeCategory.EnclosingMark)
                result.Append(char.ToLowerInvariant(ch));
        }
        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool Similar(string left, string right)
    {
        left = Key(left);
        right = Key(right);
        if (left == right || Math.Min(left.Length, right.Length) < 4) return false;

        // Distancia pequeña = posible error humano, nunca equivalencia automática.
        int max = Math.Max(left.Length, right.Length);
        int limit = max >= 8 ? 2 : 1;
        if (Math.Abs(left.Length - right.Length) > limit) return false;

        int[] previous = Enumerable.Range(0, right.Length + 1).ToArray();
        int[] current = new int[right.Length + 1];
        for (int i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[right.Length] <= limit;
    }
}
