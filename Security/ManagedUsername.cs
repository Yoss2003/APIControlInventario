using System.Globalization;
using System.Text;

namespace InventoryAPI.Security;

/// <summary>Convención MAUI: 3 caracteres del primer nombre + iniciales de apellidos.</summary>
public static class ManagedUsername
{
    public static string FromNames(string firstNames, string surnames)
    {
        var first = Clean(firstNames).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var last = Clean(surnames).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (first.Length == 0 || last.Length == 0) return "";
        var baseName = first[..Math.Min(3, first.Length)].ToLowerInvariant();
        var initials = string.Concat(last.Select(s => char.ToUpperInvariant(s[0])));
        return char.ToUpperInvariant(baseName[0]) + baseName[1..] + initials;
    }

    private static string Clean(string value)
    {
        var normalized = (value ?? "").Trim().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder();
        foreach (var letter in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letter) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(letter) || char.IsWhiteSpace(letter))
                result.Append(letter);
        }
        return result.ToString();
    }
}
