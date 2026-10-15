using System.Security.Cryptography;
using System.Text;

namespace ApiMark.Core;

/// <summary>
///     Produces and validates file and folder name segments that are legal on both Windows and Linux.
/// </summary>
/// <remarks>
///     <para>
///         All language emitters route every generated path segment through
///         <see cref="SanitizeSegment"/> so that output can be written on, and checked out onto, any
///         supported operating system. The mapping is a pure function of its input: the same raw name
///         always produces the same segment on every platform, independent of
///         <see cref="Path.GetInvalidFileNameChars"/>.
///     </para>
///     <para>
///         The rules are: <c>&lt;</c> becomes <c>{</c> and <c>&gt;</c> becomes <c>}</c> (the
///         brace notation of XML documentation identifiers); every other illegal or link-hostile
///         character (<c>: " | ? * \ /</c>, control characters, <c>[ ] ( ) # % ^ `</c>, space, and a
///         literal <c>{</c> or <c>}</c>, which must not collide with the mapped brackets) becomes
///         <c>-</c>, in which case an eight-character hexadecimal hash of the original name is appended so
///         that distinct originals stay distinct; a trailing dot or space becomes <c>-</c> (with the
///         hash); and a Windows reserved device name (<c>CON</c>, <c>PRN</c>, <c>AUX</c>, <c>NUL</c>,
///         <c>COM0-9</c>, <c>LPT0-9</c>, compared case-insensitively on the part before the first dot)
///         gets a <c>-</c> suffix on that part.
///     </para>
/// </remarks>
public static class FileNameSanitizer
{
    /// <summary>Number of hexadecimal characters of the SHA-256 hash appended to lossy substitutions.</summary>
    private const int HashLength = 8;

    /// <summary>Characters that are illegal in a Windows file name, plus link-hostile characters replaced for portability.</summary>
    private static readonly HashSet<char> ReplacedCharacters =
    [
        ':', '"', '|', '?', '*', '\\', '/', '[', ']', '(', ')', '#', '%', '^', '`', ' ',
    ];

    /// <summary>Windows reserved device names that may not be used as a file name or its base name.</summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM\u00B9", "COM\u00B2", "COM\u00B3", "LPT\u00B9", "LPT\u00B2", "LPT\u00B3",
    };

    /// <summary>
    ///     Converts an arbitrary raw name into a single path segment that is legal on Windows and Linux.
    /// </summary>
    /// <param name="rawName">The raw name; must not contain a file extension to preserve.</param>
    /// <returns>
    ///     A deterministic, legal, non-empty segment. Names that are already legal are returned unchanged
    ///     (except for the <c>&lt;</c>/<c>&gt;</c> brace mapping and the reserved-name suffix).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rawName"/> is null.</exception>
    public static string SanitizeSegment(string rawName)
    {
        ArgumentNullException.ThrowIfNull(rawName);

        var builder = new StringBuilder(rawName.Length);
        var lossy = false;
        foreach (var c in rawName)
        {
            if (c == '<')
            {
                builder.Append('{');
            }
            else if (c == '>')
            {
                builder.Append('}');
            }
            else if (ReplacedCharacters.Contains(c) || c is '{' or '}' || c < 0x20 || c == 0x7F)
            {
                // Literal braces are replaced so they cannot collide with the mapped '<' and '>'.
                builder.Append('-');
                lossy = true;
            }
            else
            {
                builder.Append(c);
            }
        }

        // Inputs that already look like one of this method's own outputs (a "-xxxxxxxx" hash tail, or a
        // reserved device name followed by the "-" suffix) are hashed so they cannot collide with it.
        if (!lossy && (HasHashTail(rawName) || LooksLikeSuffixedReservedName(rawName)))
        {
            lossy = true;
        }

        // Windows silently strips trailing dots and spaces, so they must not appear.
        while (builder.Length > 0 && builder[^1] == '.')
        {
            builder[^1] = '-';
            lossy = true;
        }

        if (builder.Length == 0)
        {
            builder.Append('_');
            lossy = rawName.Length > 0;
        }

        // Hash the original so distinct inputs that collapse to the same text stay distinct.
        if (lossy)
        {
            builder.Append('-').Append(Hash(rawName));
        }

        // A reserved device name is checked on the part before the first dot ("Con.Foo" is reserved).
        var text = builder.ToString();
        var dot = text.IndexOf('.');
        var baseName = dot < 0 ? text : text[..dot];
        if (ReservedNames.Contains(baseName.TrimEnd(' ')))
        {
            text = dot < 0 ? text + "-" : text[..dot] + "-" + text[dot..];
        }

        return text;
    }

    /// <summary>
    ///     Determines whether a name is a legal single file or folder name segment on Windows and Linux.
    /// </summary>
    /// <param name="name">The segment to test (a file name without extension, or a folder name).</param>
    /// <returns>
    ///     <see langword="true"/> when the segment is non-empty, contains none of
    ///     <c>&lt; &gt; | ? * : " / \</c> or control characters, does not end in a dot or space, and is not a
    ///     reserved device name; otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsLegalSegment(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (name.Any(c => c is '<' or '>' or '|' or '?' or '*' or ':' or '"' or '/' or '\\' || c < 0x20 || c == 0x7F))
        {
            return false;
        }

        if (name[^1] is '.' or ' ')
        {
            return false;
        }

        var dot = name.IndexOf('.');
        var baseName = dot < 0 ? name : name[..dot];
        return !ReservedNames.Contains(baseName.TrimEnd(' '));
    }

    /// <summary>Determines whether a name ends in <c>-</c> followed by exactly eight lowercase hexadecimal characters.</summary>
    /// <param name="name">The name to test.</param>
    /// <returns><see langword="true"/> when the name ends with a hash tail.</returns>
    private static bool HasHashTail(string name)
    {
        if (name.Length < HashLength + 1 || name[^(HashLength + 1)] != '-')
        {
            return false;
        }

        return name[^HashLength..].All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    /// <summary>Determines whether the part of a name before its first dot is a reserved device name plus a <c>-</c>.</summary>
    /// <param name="name">The name to test.</param>
    /// <returns><see langword="true"/> when the name equals the output form of a reserved-name rewrite.</returns>
    private static bool LooksLikeSuffixedReservedName(string name)
    {
        var dot = name.IndexOf('.');
        var baseName = dot < 0 ? name : name[..dot];
        return baseName.EndsWith('-') && ReservedNames.Contains(baseName[..^1]);
    }

    /// <summary>Computes the lowercase hexadecimal hash suffix for a raw name.</summary>
    /// <param name="rawName">The original name.</param>
    /// <returns>The first <see cref="HashLength"/> hexadecimal characters of the SHA-256 of its UTF-8 bytes.</returns>
    public static string Hash(string rawName) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawName)))[..HashLength].ToLowerInvariant();
}
