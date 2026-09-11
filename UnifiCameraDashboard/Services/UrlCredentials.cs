using System.Text.RegularExpressions;

namespace UnifiCameraDashboard.Services;

/// <summary>
/// Removes embedded credentials from URLs before they are put into an API response.
/// Unlike <see cref="LogRedaction.RedactUrlCredentials"/> this does not mask or truncate,
/// it simply drops the userinfo so the rest of the URL stays usable for diagnostics.
/// </summary>
public static class UrlCredentials
{
    // The userinfo part of the authority ("user:password@"). Greedy up to the last '@' before
    // the path or query starts, so a raw '@' inside the password is removed as well.
    private static readonly Regex UserInfoPattern = new(@"(?<=://)[^/?#\s]*@", RegexOptions.Compiled);

    /// <summary>
    /// Returns <paramref name="url"/> with any "user:password@" userinfo removed. Scheme, host,
    /// port, path and query are left untouched. Null or empty input yields an empty string.
    /// </summary>
    public static string Strip(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        return UserInfoPattern.Replace(url, string.Empty);
    }
}
