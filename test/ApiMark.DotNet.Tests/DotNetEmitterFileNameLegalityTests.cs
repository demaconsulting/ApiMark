// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>
///     Tests that every file and folder name generated for the fixtures assembly is legal on both
///     Windows and Linux, unique when compared case-insensitively, and that every relative Markdown link
///     resolves (case-sensitively) to a generated page.
/// </summary>
/// <remarks>
///     The legality rules are re-implemented here independently of the product code so the tests do not
///     trust the sanitizer they verify.
/// </remarks>
public class DotNetEmitterFileNameLegalityTests
{
    /// <summary>Matches a Markdown link and captures its target.</summary>
    private static readonly Regex LinkPattern = new(@"\]\(([^)\s]+)\)", RegexOptions.Compiled);

    /// <summary>Characters that are illegal in a Windows file name segment.</summary>
    private static readonly char[] IllegalChars = ['<', '>', '|', '?', '*', ':', '"', '/', '\\'];

    /// <summary>Windows reserved device names.</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM\u00B9", "COM\u00B2", "COM\u00B3", "LPT\u00B9", "LPT\u00B2", "LPT\u00B3",
    };

    /// <summary>Emits the fixtures assembly into an in-memory factory.</summary>
    /// <param name="visibility">The visibility level.</param>
    /// <param name="format">The output format.</param>
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory Emit(ApiVisibility visibility, OutputFormat format)
    {
        var factory = new InMemoryMarkdownWriterFactory();
        EmitTo(factory, visibility, format);
        return factory;
    }

    /// <summary>Emits the fixtures assembly into the supplied factory.</summary>
    /// <param name="factory">The target factory.</param>
    /// <param name="visibility">The visibility level.</param>
    /// <param name="format">The output format.</param>
    private static void EmitTo(IMarkdownWriterFactory factory, ApiVisibility visibility, OutputFormat format)
    {
        new DotNetGenerator(new DotNetGeneratorOptions
        {
            AssemblyPath = FixturePaths.GetFixtureDll(),
            XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
            Visibility = visibility,
        }).Parse(new InMemoryContext()).Emit(factory, new EmitConfig { Format = format }, new InMemoryContext());
    }

    /// <summary>Returns a description of why a single path segment is illegal, or null when it is legal.</summary>
    /// <param name="segment">The segment (file name with extension, or folder name).</param>
    /// <returns>The violation description, or null.</returns>
    private static string? Violation(string segment)
    {
        if (segment.Length == 0)
        {
            return "empty";
        }

        if (segment.IndexOfAny(IllegalChars) >= 0)
        {
            return "illegal character";
        }

        if (segment.Any(c => c < 0x20 || c == 0x7F))
        {
            return "control character";
        }

        if (segment.EndsWith(' ') || segment.EndsWith('.'))
        {
            return "trailing space or dot";
        }

        var dot = segment.IndexOf('.');
        var baseName = (dot < 0 ? segment : segment[..dot]).TrimEnd(' ');
        return Reserved.Contains(baseName) ? "reserved device name" : null;
    }

    /// <summary>Resolves a relative link to a normalized page key (without the <c>.md</c> extension).</summary>
    /// <param name="key">The key of the page that contains the link.</param>
    /// <param name="link">The raw link target.</param>
    /// <returns>The resolved key, or null when the link is external or an in-page anchor.</returns>
    private static string? Resolve(string key, string link)
    {
        var path = link.Split('#')[0];
        if (path.Length == 0 || path.Contains("://", StringComparison.Ordinal) || !path.EndsWith(".md", StringComparison.Ordinal))
        {
            return null;
        }

        var segments = key.Split('/').SkipLast(1).ToList();
        foreach (var part in Uri.UnescapeDataString(path)[..^3].Split('/'))
        {
            if (part == "..")
            {
                segments.RemoveAt(segments.Count - 1);
            }
            else if (part != ".")
            {
                segments.Add(part);
            }
        }

        return string.Join('/', segments);
    }

    /// <summary>Returns every text fragment that may contain an inline Markdown link.</summary>
    /// <param name="op">The recorded operation.</param>
    /// <returns>The text fragments.</returns>
    private static IEnumerable<string> TextsOf(MarkdownOperation op) => op switch
    {
        HeadingOperation h => [h.Text],
        ParagraphOperation p => [p.Text],
        SignatureOperation s => [s.Code],
        TableOperation t => t.Headers.Concat(t.Rows.SelectMany(r => r)),
        _ => [],
    };

    /// <summary>Collects every legality, collision, and link violation of an emit.</summary>
    /// <param name="factory">The populated factory.</param>
    /// <returns>The list of violation descriptions.</returns>
    private static List<string> CollectViolations(InMemoryMarkdownWriterFactory factory)
    {
        var problems = new List<string>();
        var keys = factory.Writers.Keys.ToList();

        // Every segment must be legal
        foreach (var key in keys)
        {
            var segments = key.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = i == segments.Length - 1 ? segments[i] + ".md" : segments[i];
                var violation = Violation(segment);
                if (violation != null)
                {
                    problems.Add($"ILLEGAL ({violation}): {key}");
                }
            }
        }

        // No two files, nor a file and a folder, may share a name case-insensitively
        foreach (var group in keys.GroupBy(k => k, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            problems.Add($"CASE-COLLISION (file): {string.Join(" | ", group)}");
        }

        var names = new Dictionary<string, (bool IsFolder, string Example)>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            var segments = key.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                var isFolder = i < segments.Length - 1;
                var path = string.Join('/', segments.Take(i + 1)) + (isFolder ? string.Empty : ".md");
                var existing = names.TryGetValue(path, out var value);
                if (existing && value.IsFolder != isFolder)
                {
                    problems.Add($"FILE-FOLDER-COLLISION: {path} ({value.Example} vs {key})");
                }
                else if (!existing)
                {
                    names[path] = (isFolder, key);
                }
            }
        }

        // Folder names that differ only in case are distinct keys but collide on disk
        var folders = keys.SelectMany(k =>
            {
                var s = k.Split('/');
                return Enumerable.Range(1, s.Length - 1).Select(n => string.Join('/', s.Take(n)));
            })
            .Distinct(StringComparer.Ordinal);
        foreach (var group in folders.GroupBy(f => f, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            problems.Add($"CASE-COLLISION (folder): {string.Join(" | ", group)}");
        }

        // Every relative link must resolve, with exact case, to a generated page
        var keySet = new HashSet<string>(keys, StringComparer.Ordinal);
        foreach (var (key, writer) in factory.Writers)
        {
            foreach (var op in writer.Operations)
            {
                var links = op is LinkOperation link
                    ? [link.RelativePath]
                    : TextsOf(op).SelectMany(t => LinkPattern.Matches(t).Select(m => m.Groups[1].Value)).ToList();
                foreach (var match in links)
                {
                    var target = Resolve(key, match);
                    if (target != null && !keySet.Contains(target))
                    {
                        problems.Add($"BROKEN-LINK: {key} -> {match} (resolved {target})");
                    }
                }
            }
        }

        return problems;
    }

    /// <summary>Asserts there are no violations, listing each one in the failure message.</summary>
    /// <param name="problems">The violations.</param>
    private static void AssertNone(List<string> problems) =>
        Assert.True(problems.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, problems.Distinct().OrderBy(p => p, StringComparer.Ordinal)));

    /// <summary>Validates gradual-disclosure output file names are legal, unique, and link-consistent.</summary>
    /// <param name="visibility">The visibility level to emit.</param>
    [Theory]
    [InlineData(ApiVisibility.Public)]
    [InlineData(ApiVisibility.PublicAndProtected)]
    [InlineData(ApiVisibility.All)]
    public void DotNetEmitterGradualDisclosure_Emit_AllVisibilities_FileNamesAreLegalAndLinksResolve(ApiVisibility visibility)
    {
        // Arrange / Act
        var factory = Emit(visibility, OutputFormat.GradualDisclosure);

        // Assert
        Assert.True(factory.Writers.Count > 1);
        AssertNone(CollectViolations(factory));
    }

    /// <summary>Validates single-file output produces one legal file only.</summary>
    /// <param name="visibility">The visibility level to emit.</param>
    [Theory]
    [InlineData(ApiVisibility.Public)]
    [InlineData(ApiVisibility.PublicAndProtected)]
    [InlineData(ApiVisibility.All)]
    public void DotNetEmitterSingleFile_Emit_AllVisibilities_ProducesSingleLegalFile(ApiVisibility visibility)
    {
        // Arrange / Act
        var factory = Emit(visibility, OutputFormat.SingleFile);

        // Assert
        Assert.Equal(["api"], factory.Writers.Keys);
        AssertNone(CollectViolations(factory));
    }

    /// <summary>Validates the fixtures can be written to a real file system and every file is created.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_EmitToDisk_AllVisibility_WritesEveryPageToFileSystem()
    {
        // Arrange
        var dir = Path.Combine(Path.GetTempPath(), "apimark-legality-" + Guid.NewGuid().ToString("N"));
        try
        {
            var expected = Emit(ApiVisibility.All, OutputFormat.GradualDisclosure).Writers.Keys.ToList();

            // Act
            EmitTo(new FileMarkdownWriterFactory(dir), ApiVisibility.All, OutputFormat.GradualDisclosure);

            // Assert
            var actual = Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/'))
                .Select(f => f[..^3])
                .ToList();
            Assert.Equal(expected.Count, actual.Count);
            Assert.Equal(expected.OrderBy(k => k, StringComparer.Ordinal), actual.OrderBy(k => k, StringComparer.Ordinal));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
