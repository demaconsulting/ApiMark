using System.Text;

namespace ApiMark.Core;

/// <summary>
///     Character-level rules that turn a raw symbol name into a single file or folder name segment that is
///     legal on Windows, Linux, and macOS. This is the only place where a name is escaped.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="SymbolNameRegistry"/> is the sole production caller of <see cref="Escape"/>: every
///         name is escaped exactly once, when it is registered, and every writer and link resolver then
///         uses the registry's approved name unchanged. Escaping an already escaped name would change it
///         again, which is why no other code may call <see cref="Escape"/>.
///     </para>
///     <para>
///         The rules are numbered in <c>docs/design/api-mark-core/file-naming-rules.md</c> (NR-06 to
///         NR-09). In short: <c>{</c> and <c>}</c> are structural notation that callers supply on purpose
///         (generic arity and type arguments); every other illegal or link-hostile character becomes
///         <c>_</c>; a trailing dot or space becomes <c>_</c>; a name that is empty becomes <c>_</c>; a
///         Windows reserved device name gets a <c>_</c> suffix on the part before its first dot; names are
///         normalized to Unicode form C; and a name longer than <see cref="MaxNameLength"/> characters is
///         truncated.
///     </para>
/// </remarks>
public static class FileNameRules
{
    /// <summary>The longest name, in UTF-16 code units, before the extension and any collision suffix.</summary>
    public const int MaxNameLength = 200;

    /// <summary>The character that replaces every illegal or link-hostile character.</summary>
    public const char ReplacementCharacter = '_';

    /// <summary>
    ///     The characters replaced by <see cref="Escape"/> and rejected by <see cref="IsLegalSegment"/>:
    ///     those illegal in a Windows file name, plus link-hostile characters replaced for portability (NR-06).
    /// </summary>
    private static readonly HashSet<char> ReplacedCharacters =
    [
        '<', '>', ':', '"', '|', '?', '*', '\\', '/', '[', ']', '(', ')', '#', '%', '^', '`', ' ',
    ];

    /// <summary>
    ///     Determines whether a character is replaced by <see cref="Escape"/>, that is, whether it may not
    ///     appear in a segment. Braces are structural and are not included.
    /// </summary>
    /// <param name="c">The character to test.</param>
    /// <returns><see langword="true"/> for an illegal, link-hostile, or control character.</returns>
    private static bool IsReplacedCharacter(char c) => ReplacedCharacters.Contains(c) || c < 0x20 || c == 0x7F;

    /// <summary>Windows reserved device names that may not be used as a file name or its base name.</summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM\u00B9", "COM\u00B2", "COM\u00B3", "LPT\u00B9", "LPT\u00B2", "LPT\u00B3",
    };

    /// <summary>
    ///     Escapes a raw name into a legal single path segment.
    /// </summary>
    /// <param name="rawName">
    ///     The raw name without an extension. Braces are accepted as structural notation (for example
    ///     <c>Foo{2}</c>) and are kept.
    /// </param>
    /// <param name="transformed">
    ///     Set to <see langword="true"/> when the result differs from <paramref name="rawName"/> or contains
    ///     structural braces, that is, when the name is not the plain "identity" name of the symbol.
    /// </param>
    /// <returns>A legal, non-empty segment.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rawName"/> is null.</exception>
    public static string Escape(string rawName, out bool transformed)
    {
        ArgumentNullException.ThrowIfNull(rawName);

        var normalized = rawName.Normalize(NormalizationForm.FormC);
        transformed = !string.Equals(normalized, rawName, StringComparison.Ordinal);

        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var isStructural = c is '{' or '}';
            var isReplaced = !isStructural && IsReplacedCharacter(c);
            builder.Append(isReplaced ? ReplacementCharacter : c);
            transformed |= isStructural || isReplaced;
        }

        // Windows silently strips trailing dots and spaces, so they must not appear.
        if (builder.Length > 0 && builder[^1] == '.')
        {
            builder[^1] = ReplacementCharacter;
            transformed = true;
        }

        if (builder.Length == 0)
        {
            builder.Append(ReplacementCharacter);
            transformed = true;
        }

        if (builder.Length > MaxNameLength)
        {
            var length = char.IsHighSurrogate(builder[MaxNameLength - 1]) ? MaxNameLength - 1 : MaxNameLength;
            builder.Length = length;
            transformed = true;
        }

        var text = builder.ToString();

        // A reserved device name is checked on the part before the first dot ("Con.Foo" is reserved).
        var dot = text.IndexOf('.');
        var baseName = dot < 0 ? text : text[..dot];
        if (ReservedNames.Contains(baseName))
        {
            text = dot < 0 ? text + ReplacementCharacter : text[..dot] + ReplacementCharacter + text[dot..];
            transformed = true;
        }

        return text;
    }

    /// <summary>
    ///     Determines whether a name is a legal single file or folder name segment on Windows and Linux.
    /// </summary>
    /// <param name="name">The segment to test (a file name without extension, or a folder name).</param>
    /// <returns>
    ///     <see langword="true"/> when the segment is non-empty, contains none of the characters
    ///     <see cref="Escape"/> replaces (<c>&lt; &gt; : " | ? * \ /</c>, space, <c>[ ] ( ) # % ^</c>, the
    ///     backtick, and control characters), does not end in a dot or space, and is not a reserved device
    ///     name; otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsLegalSegment(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (name.Any(IsReplacedCharacter))
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
}
