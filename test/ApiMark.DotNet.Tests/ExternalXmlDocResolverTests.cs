using System.Reflection;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="ExternalXmlDocResolver"/>.</summary>
public class ExternalXmlDocResolverTests
{
    /// <summary>
    ///     Creates a temporary directory for a single test, returning its path so the caller can
    ///     clean it up after use.
    /// </summary>
    /// <returns>Path to a newly created, empty temporary directory.</returns>
    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ApiMarkTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Writes a minimal XML documentation file at <paramref name="path"/> containing <paramref name="membersXml"/>.</summary>
    /// <param name="path">Destination path for the XML documentation file.</param>
    /// <param name="membersXml">Raw XML to embed inside the &lt;members&gt; element.</param>
    private static void WriteXmlDoc(string path, string membersXml)
    {
        var xml = $"""
            <?xml version="1.0"?>
            <doc>
              <assembly><name>TestAssembly</name></assembly>
              <members>
                {membersXml}
              </members>
            </doc>
            """;
        File.WriteAllText(path, xml);
    }

    /// <summary>
    ///     Validates that <see cref="ExternalXmlDocResolver.TryGetMember"/> resolves a member from
    ///     the conventional sibling <c>.xml</c> file next to the reference assembly DLL.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMember_SiblingXmlPresent_ReturnsMember()
    {
        // Arrange
        var dir = CreateTempDirectory();
        try
        {
            var dllPath = Path.Combine(dir, "Foo.dll");
            File.WriteAllBytes(dllPath, []);
            WriteXmlDoc(Path.ChangeExtension(dllPath, ".xml"), """
                <member name="T:Foo.Bar">
                    <summary>Sibling summary text.</summary>
                </member>
                """);
            var sut = new ExternalXmlDocResolver([dllPath]);

            // Act
            var member = sut.TryGetMember("T:Foo.Bar");

            // Assert
            Assert.NotNull(member);
            Assert.Equal("Sibling summary text.", member.Element("summary")?.Value.Trim());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that <see cref="ExternalXmlDocResolver.TryGetMember"/> returns <c>null</c>
    ///     when no XML documentation file exists anywhere for the configured reference path.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMember_NoDocAnywhere_ReturnsNull()
    {
        // Arrange
        var dir = CreateTempDirectory();
        try
        {
            var dllPath = Path.Combine(dir, "Foo.dll");
            File.WriteAllBytes(dllPath, []);
            var sut = new ExternalXmlDocResolver([dllPath]);

            // Act
            var member = sut.TryGetMember("T:Foo.Bar");

            // Assert
            Assert.Null(member);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that <see cref="ExternalXmlDocResolver.TryGetMember"/> falls back to a
    ///     <c>lib/</c> folder XML doc file when the reference assembly DLL sits under a <c>ref/</c>
    ///     folder with no sibling XML doc of its own.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMember_RefLibFolderSwap_LibXmlPresentUnderRefDll_ReturnsMember()
    {
        // Arrange
        var dir = CreateTempDirectory();
        try
        {
            var refDir = Path.Combine(dir, "ref", "net8.0");
            var libDir = Path.Combine(dir, "lib", "net8.0");
            Directory.CreateDirectory(refDir);
            Directory.CreateDirectory(libDir);

            var refDllPath = Path.Combine(refDir, "Foo.dll");
            File.WriteAllBytes(refDllPath, []);
            WriteXmlDoc(Path.Combine(libDir, "Foo.xml"), """
                <member name="T:Foo.Bar">
                    <summary>Lib summary text.</summary>
                </member>
                """);
            var sut = new ExternalXmlDocResolver([refDllPath]);

            // Act
            var member = sut.TryGetMember("T:Foo.Bar");

            // Assert
            Assert.NotNull(member);
            Assert.Equal("Lib summary text.", member.Element("summary")?.Value.Trim());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that <see cref="ExternalXmlDocResolver.TryGetMember"/> falls back to a
    ///     <c>ref/</c> folder XML doc file when the reference assembly DLL sits under a <c>lib/</c>
    ///     folder with no sibling XML doc of its own.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMember_RefLibFolderSwap_RefXmlPresentUnderLibDll_ReturnsMember()
    {
        // Arrange
        var dir = CreateTempDirectory();
        try
        {
            var refDir = Path.Combine(dir, "ref", "net8.0");
            var libDir = Path.Combine(dir, "lib", "net8.0");
            Directory.CreateDirectory(refDir);
            Directory.CreateDirectory(libDir);

            var libDllPath = Path.Combine(libDir, "Foo.dll");
            File.WriteAllBytes(libDllPath, []);
            WriteXmlDoc(Path.Combine(refDir, "Foo.xml"), """
                <member name="T:Foo.Bar">
                    <summary>Ref summary text.</summary>
                </member>
                """);
            var sut = new ExternalXmlDocResolver([libDllPath]);

            // Act
            var member = sut.TryGetMember("T:Foo.Bar");

            // Assert
            Assert.NotNull(member);
            Assert.Equal("Ref summary text.", member.Element("summary")?.Value.Trim());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that the <c>ref/</c>&#8596;<c>lib/</c> folder-segment swap targets the segment
    ///     nearest the assembly file (the actual NuGet package-layout segment), not an unrelated,
    ///     earlier path segment that happens to also be named <c>ref</c> or <c>lib</c> — for
    ///     example a decoy <c>lib</c> folder in the path leading up to a real NuGet package cache
    ///     directory, mirroring a real-world layout such as
    ///     <c>/home/lib/.nuget/packages/Pkg/ref/net8.0/Pkg.dll</c>.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMember_RefLibFolderSwap_DecoySegmentEarlierInPath_SwapsSegmentNearestDll()
    {
        // Arrange: an earlier, unrelated "lib" segment (simulating e.g. "/home/lib/...") sits
        // before the real NuGet package-layout "ref"/"net8.0" segments nearest the DLL.
        var dir = CreateTempDirectory();
        try
        {
            var decoyLibDir = Path.Combine(dir, "lib", ".nuget", "packages", "Pkg");
            var refDir = Path.Combine(decoyLibDir, "ref", "net8.0");
            var realLibDir = Path.Combine(decoyLibDir, "lib", "net8.0");
            Directory.CreateDirectory(refDir);
            Directory.CreateDirectory(realLibDir);

            var refDllPath = Path.Combine(refDir, "Pkg.dll");
            File.WriteAllBytes(refDllPath, []);
            WriteXmlDoc(Path.Combine(realLibDir, "Pkg.xml"), """
                <member name="T:Pkg.Bar">
                    <summary>Real lib summary text.</summary>
                </member>
                """);
            var sut = new ExternalXmlDocResolver([refDllPath]);

            // Act
            var member = sut.TryGetMember("T:Pkg.Bar");

            // Assert: resolution must find the XML doc under the "ref"-to-"lib" swap of the
            // segment nearest the DLL, not a swap of the earlier decoy "lib" segment (which would
            // produce a nonexistent probe path and leave the member unresolved).
            Assert.NotNull(member);
            Assert.Equal("Real lib summary text.", member.Element("summary")?.Value.Trim());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that <see cref="ExternalXmlDocResolver.TryGetMember"/> searches all
    ///     configured reference paths in order, using the second path's documentation when the
    ///     member is absent from the first path's documentation.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMember_SecondReferencePathMatches_ReturnsMemberFromSecondPath()
    {
        // Arrange
        var dir = CreateTempDirectory();
        try
        {
            var firstDllPath = Path.Combine(dir, "First.dll");
            File.WriteAllBytes(firstDllPath, []);
            WriteXmlDoc(Path.ChangeExtension(firstDllPath, ".xml"), """
                <member name="T:Foo.Other">
                    <summary>Unrelated member.</summary>
                </member>
                """);

            var secondDllPath = Path.Combine(dir, "Second.dll");
            File.WriteAllBytes(secondDllPath, []);
            WriteXmlDoc(Path.ChangeExtension(secondDllPath, ".xml"), """
                <member name="T:Foo.Bar">
                    <summary>Second path summary text.</summary>
                </member>
                """);

            var sut = new ExternalXmlDocResolver([firstDllPath, secondDllPath]);

            // Act
            var member = sut.TryGetMember("T:Foo.Bar");

            // Assert
            Assert.NotNull(member);
            Assert.Equal("Second path summary text.", member.Element("summary")?.Value.Trim());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates the perf-motivated fast path of
    ///     <see cref="ExternalXmlDocResolver.TryGetMember(string, string?)"/>: when a correct
    ///     <c>declaringAssemblyHint</c> is supplied, only the hinted reference path's XML
    ///     documentation is ever parsed — the other configured reference paths (simulating an
    ///     MSBuild auto-harvested <c>ReferencePaths</c> list containing many unrelated
    ///     dependencies) are never touched. This is confirmed via reflection on the private
    ///     <c>_docsByReferencePath</c> cache, which must contain exactly one entry after the call.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMemberWithHint_CorrectHint_OnlyProbesHintedPathNotOthers()
    {
        // Arrange
        var dir = CreateTempDirectory();
        try
        {
            var unrelatedPaths = new List<string>();
            for (var i = 0; i < 5; i++)
            {
                var unrelatedDllPath = Path.Combine(dir, $"Unrelated{i}.dll");
                File.WriteAllBytes(unrelatedDllPath, []);
                WriteXmlDoc(Path.ChangeExtension(unrelatedDllPath, ".xml"), """
                    <member name="T:Foo.Bar">
                        <summary>Wrong summary from an unrelated dependency.</summary>
                    </member>
                    """);
                unrelatedPaths.Add(unrelatedDllPath);
            }

            var hintedDllPath = Path.Combine(dir, "Hinted.dll");
            File.WriteAllBytes(hintedDllPath, []);
            WriteXmlDoc(Path.ChangeExtension(hintedDllPath, ".xml"), """
                <member name="T:Foo.Bar">
                    <summary>Correct summary from the hinted dependency.</summary>
                </member>
                """);

            // The hinted path is placed LAST so that an in-order fallback scan would find one of
            // the unrelated (wrong) matches first, distinguishing the fast path from a full scan.
            var sut = new ExternalXmlDocResolver([.. unrelatedPaths, hintedDllPath]);

            // Act
            var member = sut.TryGetMember("T:Foo.Bar", declaringAssemblyHint: "Hinted");

            // Assert: the hinted (correct) member is returned, not an unrelated one
            Assert.NotNull(member);
            Assert.Equal("Correct summary from the hinted dependency.", member.Element("summary")?.Value.Trim());

            // Assert: only the hinted reference path was ever parsed and cached; none of the
            // unrelated paths were probed
            var cacheField = typeof(ExternalXmlDocResolver).GetField("_docsByReferencePath", BindingFlags.NonPublic | BindingFlags.Instance);
            var cache = Assert.IsAssignableFrom<System.Collections.IDictionary>(cacheField!.GetValue(sut));
            Assert.Single(cache);
            var cachedKey = Assert.Single(cache.Keys.Cast<string>());
            Assert.Equal(Path.GetFileNameWithoutExtension(hintedDllPath), Path.GetFileNameWithoutExtension(cachedKey));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that <see cref="ExternalXmlDocResolver.TryGetMember(string, string?)"/> falls
    ///     back to the full in-order scan of every configured reference path when the supplied
    ///     hint does not match any configured path's file name — for example a stale or incorrect
    ///     hint — so a wrong hint never causes a real, resolvable match to be missed.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMemberWithHint_HintMatchesNoConfiguredPath_FallsBackToFullScan()
    {
        // Arrange
        var dir = CreateTempDirectory();
        try
        {
            var dllPath = Path.Combine(dir, "Actual.dll");
            File.WriteAllBytes(dllPath, []);
            WriteXmlDoc(Path.ChangeExtension(dllPath, ".xml"), """
                <member name="T:Foo.Bar">
                    <summary>Resolved via fallback scan.</summary>
                </member>
                """);

            var sut = new ExternalXmlDocResolver([dllPath]);

            // Act: the hint names an assembly that is not among the configured reference paths
            var member = sut.TryGetMember("T:Foo.Bar", declaringAssemblyHint: "NoSuchAssembly");

            // Assert
            Assert.NotNull(member);
            Assert.Equal("Resolved via fallback scan.", member.Element("summary")?.Value.Trim());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that <see cref="ExternalXmlDocResolver"/> parses each reference assembly's
    ///     XML documentation file at most once and caches the result: deleting the file from disk
    ///     after a successful lookup does not prevent a second, different member from the same
    ///     file resolving successfully.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMember_CachesParsedDocAcrossCalls_DeletedFileStillResolvesSecondCall()
    {
        // Arrange
        var dir = CreateTempDirectory();
        try
        {
            var dllPath = Path.Combine(dir, "Foo.dll");
            File.WriteAllBytes(dllPath, []);
            var xmlPath = Path.ChangeExtension(dllPath, ".xml");
            WriteXmlDoc(xmlPath, """
                <member name="T:Foo.Bar">
                    <summary>First summary text.</summary>
                </member>
                <member name="T:Foo.Baz">
                    <summary>Second summary text.</summary>
                </member>
                """);
            var sut = new ExternalXmlDocResolver([dllPath]);

            // Act: resolve one member, then delete the XML file, then resolve a different member
            // from the same file — the second call must still succeed because the file was
            // parsed and cached on the first access rather than re-read from disk.
            var first = sut.TryGetMember("T:Foo.Bar");
            File.Delete(xmlPath);
            var second = sut.TryGetMember("T:Foo.Baz");

            // Assert
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal("Second summary text.", second.Element("summary")?.Value.Trim());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that the <c>ref/</c>&#8596;<c>lib/</c> folder-segment swap always matches the
    ///     <c>ref</c>/<c>lib</c> segment names case-insensitively, regardless of platform or file
    ///     system: a segment spelled with different casing than exactly <c>ref</c> (e.g.
    ///     <c>REF</c>) is still recognized, because this check recognizes a known NuGet layout
    ///     convention token rather than deciding whether two independently supplied real paths
    ///     name the same on-disk file (see the platform-independence remarks on
    ///     <see cref="ExternalXmlDocResolver"/>'s private <c>SwapRefLibSegment</c> method).
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMember_RefLibFolderSwap_DifferentCaseSegment_AlwaysMatchesCaseInsensitively()
    {
        // Arrange: the ref-side directory segment is spelled "REF" (different case than the
        // exactly-lowercase "lib" segment created alongside it).
        var dir = CreateTempDirectory();
        try
        {
            var refDir = Path.Combine(dir, "REF", "net8.0");
            var libDir = Path.Combine(dir, "lib", "net8.0");
            Directory.CreateDirectory(refDir);
            Directory.CreateDirectory(libDir);

            var refDllPath = Path.Combine(refDir, "Foo.dll");
            File.WriteAllBytes(refDllPath, []);
            WriteXmlDoc(Path.Combine(libDir, "Foo.xml"), """
                <member name="T:Foo.Bar">
                    <summary>Lib summary text.</summary>
                </member>
                """);
            var sut = new ExternalXmlDocResolver([refDllPath]);

            // Act
            var member = sut.TryGetMember("T:Foo.Bar");

            // Assert: "REF" is recognized as the "ref" segment on every platform, so the swap
            // occurs and the member resolves regardless of the current platform or file system.
            Assert.NotNull(member);
            Assert.Equal("Lib summary text.", member.Element("summary")?.Value.Trim());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that <see cref="ExternalXmlDocResolver"/> normalizes reference assembly paths
    ///     at construction time so that two different string forms of the SAME underlying file —
    ///     here, a canonical absolute path and a second absolute path that lexically collapses to
    ///     the same location via a <c>..</c> segment — collapse to a single
    ///     <c>_docsByReferencePath</c> cache key, preserving the documented "parsed at most once"
    ///     guarantee.
    /// </summary>
    /// <remarks>
    ///     A functional (input/output only) test cannot distinguish this from the pre-fix
    ///     behavior: once either string form is parsed, its resulting member dictionary already
    ///     contains every member declared in the file, so any subsequent lookup succeeds via the
    ///     first cached entry regardless of whether the second, differently-spelled path was
    ///     ever independently cached. The only reliable way to prove the cache key collapsed to
    ///     one entry (rather than two) is to inspect the private cache dictionary directly via
    ///     reflection, which this test does using a query that misses (so the resolver must
    ///     attempt every configured path before giving up, touching both string forms).
    /// </remarks>
    [Fact]
    public void ExternalXmlDocResolver_Constructor_RelativeAndAbsoluteFormsOfSamePath_ShareSingleCacheKey()
    {
        // Arrange
        var dir = CreateTempDirectory();
        try
        {
            var dllPath = Path.Combine(dir, "Foo.dll");
            File.WriteAllBytes(dllPath, []);
            WriteXmlDoc(Path.ChangeExtension(dllPath, ".xml"), """
                <member name="T:Foo.Bar">
                    <summary>Summary text.</summary>
                </member>
                """);

            // Configure the SAME underlying file via two different absolute string forms: the
            // canonical form, and a second form that lexically collapses to the same location via
            // a "Nested/.." segment — Path.GetFullPath performs this lexical collapse without
            // requiring the intermediate "Nested" directory to actually exist on disk.
            var canonicalDllPath = Path.Combine(dir, "Foo.dll");
            var lexicallyCollapsingDllPath = Path.Combine(dir, "Nested", "..", "Foo.dll");
            var sut = new ExternalXmlDocResolver([canonicalDllPath, lexicallyCollapsingDllPath]);

            // Act: query a member ID that exists in neither file so the resolver must walk every
            // configured reference path (rather than short-circuiting on the first match),
            // populating _docsByReferencePath for both configured entries.
            var miss = sut.TryGetMember("T:Foo.DoesNotExist");

            // Assert: the miss lookup behaves as documented, and — the actual point of this test —
            // the private per-path cache dictionary has exactly ONE entry, proving the two
            // differently-spelled absolute forms of the same path collapsed to a single normalized
            // cache key rather than being independently cached (and, by extension, independently
            // parsed) as two distinct reference paths.
            Assert.Null(miss);
            var cacheField = typeof(ExternalXmlDocResolver).GetField("_docsByReferencePath", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(cacheField);
            var cache = Assert.IsAssignableFrom<System.Collections.IDictionary>(cacheField!.GetValue(sut));
            _ = Assert.Single(cache.Keys.Cast<object>());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that <see cref="ExternalXmlDocResolver.TryGetMember"/> returns <c>null</c>
    ///     when constructed with an empty reference-path list.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMember_EmptyReferencePathList_ReturnsNull()
    {
        // Arrange
        var sut = new ExternalXmlDocResolver([]);

        // Act
        var member = sut.TryGetMember("T:Foo.Bar");

        // Assert
        Assert.Null(member);
    }

    /// <summary>
    ///     Validates that <see cref="ExternalXmlDocResolver.TryGetMember"/> returns <c>null</c>
    ///     instead of throwing when a reference assembly's sibling <c>.xml</c> file exists but
    ///     contains malformed/corrupt XML.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMember_CorruptXmlDocFile_ReturnsNullWithoutThrowing()
    {
        // Arrange
        var dir = CreateTempDirectory();
        try
        {
            var dllPath = Path.Combine(dir, "Foo.dll");
            File.WriteAllBytes(dllPath, []);
            // Deliberately malformed XML (unclosed tags) — a well-known reliable trigger for
            // System.Xml.XmlException when parsed via XDocument.Load.
            File.WriteAllText(Path.ChangeExtension(dllPath, ".xml"), "<doc><members><member name=\"T:Foo.Bar\">");
            var sut = new ExternalXmlDocResolver([dllPath]);

            // Act
            var member = sut.TryGetMember("T:Foo.Bar");

            // Assert: no exception propagated (a thrown exception would fail this test before
            // reaching the assertion), and the lookup degrades to "not found".
            Assert.Null(member);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that a repeated <see cref="ExternalXmlDocResolver.TryGetMember"/> miss for
    ///     the same member ID is served from the negative per-member cache without re-reading the
    ///     underlying XML documentation file from disk.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMember_RepeatedMissForSameMember_DoesNotReReadDiskAfterFirstMiss()
    {
        // Arrange
        var dir = CreateTempDirectory();
        try
        {
            var dllPath = Path.Combine(dir, "Foo.dll");
            File.WriteAllBytes(dllPath, []);
            var xmlPath = Path.ChangeExtension(dllPath, ".xml");
            WriteXmlDoc(xmlPath, """
                <member name="T:Foo.Other">
                    <summary>Unrelated member.</summary>
                </member>
                """);
            var sut = new ExternalXmlDocResolver([dllPath]);

            // Act: first call is a miss (populates both caches with a negative result). Then
            // rewrite (rather than delete) the underlying XML file so it now DOES contain a
            // matching entry for the same member ID, and call again for that member ID — a
            // non-caching implementation that simply re-reads the file would find the new entry
            // and return non-null, so asserting the second call still returns null proves the
            // negative result came from the cache rather than a fresh disk read.
            var first = sut.TryGetMember("T:Foo.Missing");
            WriteXmlDoc(xmlPath, """
                <member name="T:Foo.Missing">
                    <summary>Now present, but must not be observed due to caching.</summary>
                </member>
                """);
            var second = sut.TryGetMember("T:Foo.Missing");

            // Assert
            Assert.Null(first);
            Assert.Null(second);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that <see cref="ExternalXmlDocResolver"/> snapshots the reference-path list
    ///     at construction time: mutating the caller's original <see cref="List{T}"/> instance
    ///     after construction (e.g. adding a new path) must not be observed, because a live
    ///     reference to the caller's list would let a newly added path silently invalidate the
    ///     negative per-member cache populated before the mutation.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_TryGetMember_ReferencePathListMutatedAfterConstruction_NewPathNotObserved()
    {
        // Arrange
        var dir = CreateTempDirectory();
        try
        {
            var firstDllPath = Path.Combine(dir, "First.dll");
            File.WriteAllBytes(firstDllPath, []);
            WriteXmlDoc(Path.ChangeExtension(firstDllPath, ".xml"), """
                <member name="T:Foo.Other">
                    <summary>Unrelated member.</summary>
                </member>
                """);

            var mutableReferencePaths = new List<string> { firstDllPath };
            var sut = new ExternalXmlDocResolver(mutableReferencePaths);

            // Act: first lookup misses and is cached as "not found" while only First.dll is
            // configured. Then a second reference assembly containing the member is added to the
            // SAME list instance the resolver was constructed with, and the lookup is repeated —
            // a resolver holding a live reference to the caller's list would pick up the new path
            // and find the member; a resolver that snapshotted the list at construction time must
            // still return null because it never observes the mutation.
            var beforeMutation = sut.TryGetMember("T:Foo.Target");

            var secondDllPath = Path.Combine(dir, "Second.dll");
            File.WriteAllBytes(secondDllPath, []);
            WriteXmlDoc(Path.ChangeExtension(secondDllPath, ".xml"), """
                <member name="T:Foo.Target">
                    <summary>Now present, but only via a path added after construction.</summary>
                </member>
                """);
            mutableReferencePaths.Add(secondDllPath);

            var afterMutation = sut.TryGetMember("T:Foo.Target");

            // Assert: both lookups miss because the resolver is immune to mutation of the
            // original list instance after construction.
            Assert.Null(beforeMutation);
            Assert.Null(afterMutation);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that the <see cref="ExternalXmlDocResolver"/> constructor throws
    ///     <see cref="ArgumentNullException"/> when given a null reference-path list.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_Constructor_NullReferencePaths_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: constructing with a null list must fail fast
        Assert.Throws<ArgumentNullException>(() => new ExternalXmlDocResolver(null!));
    }

    /// <summary>
    ///     Validates that blank (empty/whitespace-only) entries in the reference-path list are
    ///     filtered out at construction time rather than being normalized into a bogus,
    ///     legitimate-looking reference path equal to the current working directory (which is
    ///     what <see cref="Path.GetFullPath(string)"/> would otherwise resolve an empty string to).
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_Constructor_BlankAndWhitespacePaths_IgnoredWithoutThrowing()
    {
        // Arrange / Act: construct with only empty/whitespace entries, no real paths at all
        var sut = new ExternalXmlDocResolver(["", "   "]);
        var member = sut.TryGetMember("T:Anything");

        // Assert: construction does not throw, the lookup misses, and reflection on the private
        // field proves the blank entries were dropped rather than becoming a bogus CWD-equivalent
        // search path
        Assert.Null(member);
        var pathsField = typeof(ExternalXmlDocResolver).GetField("_referenceAssemblyPaths", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(pathsField);
        var paths = Assert.IsAssignableFrom<System.Collections.ICollection>(pathsField!.GetValue(sut));
        Assert.Empty(paths);
    }
}
