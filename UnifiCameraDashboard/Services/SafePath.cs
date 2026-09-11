namespace UnifiCameraDashboard.Services;

/// <summary>
/// Guards for file paths that are built from request data (camera ids, HLS segment names).
/// </summary>
public static class SafePath
{
    private const int MaxSegmentLength = 128;

    /// <summary>
    /// True if <paramref name="segment"/> is a single, plain path segment - no directory
    /// separators, no drive letters, no "." or ".." traversal.
    /// </summary>
    public static bool IsSafeSegment(string? segment)
    {
        if (string.IsNullOrEmpty(segment) || segment.Length > MaxSegmentLength)
            return false;

        // A leading dot also rules out "." and ".." without a special case for them.
        if (segment[0] == '.')
            return false;

        foreach (var c in segment)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
                return false;
        }

        return true;
    }

    /// <summary>
    /// Combines <paramref name="segments"/> onto <paramref name="baseDir"/> and returns the
    /// resulting absolute path only if every segment is safe and the result stays inside the
    /// base directory. Returns <c>null</c> otherwise.
    /// </summary>
    public static string? CombineUnder(string baseDir, params string[] segments)
    {
        foreach (var segment in segments)
        {
            if (!IsSafeSegment(segment))
                return null;
        }

        var root = Path.GetFullPath(baseDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var parts = new string[segments.Length + 1];
        parts[0] = root;
        Array.Copy(segments, 0, parts, 1, segments.Length);

        var combined = Path.GetFullPath(Path.Combine(parts));

        return combined.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? combined
            : null;
    }
}
