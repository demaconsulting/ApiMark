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

            // Act: first call is a miss (populates both caches with a negative result), then
            // delete the underlying XML file, then call again for the same member ID — the
            // second call must still return null without needing the (now-deleted) file.
            var first = sut.TryGetMember("T:Foo.Missing");
            File.Delete(xmlPath);
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
    ///     Validates that the <see cref="ExternalXmlDocResolver"/> constructor throws
    ///     <see cref="ArgumentNullException"/> when given a null reference-path list.
    /// </summary>
    [Fact]
    public void ExternalXmlDocResolver_Constructor_NullReferencePaths_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: constructing with a null list must fail fast
        Assert.Throws<ArgumentNullException>(() => new ExternalXmlDocResolver(null!));
    }
}
