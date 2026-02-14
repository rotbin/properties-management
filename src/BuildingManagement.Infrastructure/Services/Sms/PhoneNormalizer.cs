using System.Text.RegularExpressions;

namespace BuildingManagement.Infrastructure.Services.Sms;

/// <summary>
/// Normalizes Israeli phone numbers to E.164 format (+9725XXXXXXXX).
/// </summary>
public static partial class PhoneNormalizer
{
    /// <summary>
    /// Normalize an Israeli phone number to E.164 format.
    /// Returns null if phone is invalid/empty.
    /// Examples:
    ///   050-1234567  -> +972501234567
    ///   0501234567   -> +972501234567
    ///   +972501234567 -> +972501234567
    ///   972501234567 -> +972501234567
    /// </summary>
    public static string? NormalizeIsraeli(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        // Strip all non-digit chars except leading +
        var cleaned = StripNonDigits().Replace(phone.Trim(), "");
        if (phone.TrimStart().StartsWith('+'))
            cleaned = "+" + cleaned;

        // Already E.164 with +972
        if (cleaned.StartsWith("+972") && cleaned.Length >= 13)
            return cleaned;

        // Starts with 972 (no +)
        if (cleaned.StartsWith("972") && cleaned.Length >= 12)
            return "+" + cleaned;

        // Starts with 0 (local Israeli format)
        if (cleaned.StartsWith('0') && cleaned.Length >= 10)
            return "+972" + cleaned[1..];

        return null; // Invalid
    }

    /// <summary>Check if an E.164 phone is a valid Israeli mobile number.</summary>
    public static bool IsValidIsraeliMobile(string? e164Phone)
    {
        if (string.IsNullOrEmpty(e164Phone)) return false;
        // Israeli mobile: +9725X followed by 7 digits => total 13 chars
        return IsraeliMobilePattern().IsMatch(e164Phone);
    }

    [GeneratedRegex(@"[^\d]")]
    private static partial Regex StripNonDigits();

    [GeneratedRegex(@"^\+9725[0-9]\d{7}$")]
    private static partial Regex IsraeliMobilePattern();
}
