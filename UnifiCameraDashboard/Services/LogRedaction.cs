using System.Text;
using System.Text.RegularExpressions;

namespace UnifiCameraDashboard.Services;

/// <summary>
/// Helpers for putting request- or device-supplied values into log messages.
/// </summary>
public static class LogRedaction
{
    private const int MaxLength = 200;

    // Matches the "user:password@" userinfo part of a URL (rtsp://, https://, ...).
    private static readonly Regex UserInfoPattern = new(@"://[^/\s@""]*@", RegexOptions.Compiled);

    /// <summary>
    /// Neutralises an untrusted value for logging: newlines and other control characters are
    /// what makes log forging possible (injected text can pose as its own log line), so they
    /// are removed and the value length is capped.
    /// </summary>
    public static string ForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var sanitized = value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

        var builder = new StringBuilder(Math.Min(sanitized.Length, MaxLength) + 3);
        foreach (var c in sanitized)
        {
            if (builder.Length >= MaxLength)
            {
                builder.Append("...");
                break;
            }

            builder.Append(char.IsControl(c) ? '_' : c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Removes embedded credentials from any URL in <paramref name="value"/> and then makes the
    /// result safe to log. Used for RTSP URLs, which carry the Protect password inline.
    /// </summary>
    public static string RedactUrlCredentials(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return ForLog(UserInfoPattern.Replace(value, "://***@"));
    }
}
