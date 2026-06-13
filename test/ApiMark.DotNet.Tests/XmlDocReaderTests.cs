using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="XmlDocReader"/>.</summary>
public class XmlDocReaderTests
{
    /// <summary>
    ///     Writes a minimal XML doc file containing <paramref name="membersXml"/> and returns the
    ///     path so the caller can clean it up after use.
    /// </summary>
    /// <param name="membersXml">Raw XML to embed inside the &lt;members&gt; element.</param>
    /// <returns>Path to the temporary XML documentation file.</returns>
    private static string WriteXmlDoc(string membersXml)
    {
        var path = Path.GetTempFileName();
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
        return path;
    }

    /// <summary>Validates that constructing an <see cref="XmlDocReader"/> with a missing file throws <see cref="FileNotFoundException"/>.</summary>
    [Fact]
    public void XmlDocReader_Constructor_FileDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange / Act / Assert
        Assert.Throws<FileNotFoundException>(() => new XmlDocReader("/nonexistent/path.xml"));
    }

    /// <summary>Validates that <see cref="XmlDocReader.GetSummary"/> returns trimmed summary text for a known member.</summary>
    [Fact]
    public void XmlDocReader_GetSummary_MemberPresent_ReturnsTrimmedText()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="T:Foo.Bar">
              <summary>  A bar class.  </summary>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);

            // Assert
            Assert.Equal("A bar class.", reader.GetSummary("T:Foo.Bar"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates that <see cref="XmlDocReader.GetSummary"/> preserves inline symbol references and language keywords.</summary>
    [Fact]
    public void XmlDocReader_GetSummary_WithInlineReferences_PreservesReferencedNames()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:Foo.Bar.IsPassed(Foo.SampleStatus)">
              <summary>Returns <see langword="true"/> when <paramref name="status"/> is <see cref="F:Foo.SampleStatus.Active"/> or <see cref="F:Foo.SampleStatus.Pending"/>.</summary>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);

            // Assert
            Assert.Equal(
                "Returns true when status is Active or Pending.",
                reader.GetSummary("M:Foo.Bar.IsPassed(Foo.SampleStatus)"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates that <see cref="XmlDocReader.GetSummary"/> returns null for a member not in the XML doc file.</summary>
    [Fact]
    public void XmlDocReader_GetSummary_MemberAbsent_ReturnsNull()
    {
        // Arrange
        var path = WriteXmlDoc(string.Empty);
        try
        {
            // Act
            var reader = new XmlDocReader(path);

            // Assert
            Assert.Null(reader.GetSummary("T:Missing.Type"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates that <see cref="XmlDocReader.GetRemarks"/> returns trimmed remarks text for a known member.</summary>
    [Fact]
    public void XmlDocReader_GetRemarks_MemberPresent_ReturnsTrimmedText()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:Foo.Bar.Compute">
              <summary>Summary.</summary>
              <remarks>  Some remarks.  </remarks>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);

            // Assert
            Assert.Equal("Some remarks.", reader.GetRemarks("M:Foo.Bar.Compute"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates that <see cref="XmlDocReader.GetExceptions"/> returns all documented exception cref values.</summary>
    [Fact]
    public void XmlDocReader_GetExceptions_MemberWithExceptions_ReturnsCrefValues()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:Foo.Bar.Open(System.String)">
              <exception cref="T:System.InvalidOperationException">Already open.</exception>
              <exception cref="T:System.ArgumentNullException">host is null.</exception>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var exs = reader.GetExceptions("M:Foo.Bar.Open(System.String)");

            // Assert
            Assert.Equal(2, exs.Count);
            Assert.Contains("T:System.InvalidOperationException", exs);
            Assert.Contains("T:System.ArgumentNullException", exs);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates that <see cref="XmlDocReader.GetParams"/> returns parameter names and descriptions in order.</summary>
    [Fact]
    public void XmlDocReader_GetParams_MemberWithParams_ReturnsNamesAndDescriptions()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:Foo.Bar.Go(System.String,System.Int32)">
              <param name="host">The host.</param>
              <param name="port">The port.</param>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var ps = reader.GetParams("M:Foo.Bar.Go(System.String,System.Int32)");

            // Assert
            Assert.Equal(2, ps.Count);
            Assert.Equal("host", ps[0].Name);
            Assert.Equal("The host.", ps[0].Description);
            Assert.Equal("port", ps[1].Name);
            Assert.Equal("The port.", ps[1].Description);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates that <see cref="XmlDocReader.GetReturns"/> returns trimmed returns text for a known member.</summary>
    [Fact]
    public void XmlDocReader_GetReturns_MemberWithReturns_ReturnsTrimmedText()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:Foo.Bar.Compute">
              <returns>  The result.  </returns>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);

            // Assert
            Assert.Equal("The result.", reader.GetReturns("M:Foo.Bar.Compute"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates that <see cref="XmlDocReader.GetExample"/> returns trimmed example text for a known member.</summary>
    [Fact]
    public void XmlDocReader_GetExample_MemberWithExample_ReturnsTrimmedText()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:Foo.Bar.Sample">
              <example>  var x = new Bar();  </example>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);

            // Assert
            Assert.Equal("var x = new Bar();", reader.GetExample("M:Foo.Bar.Sample"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates that <see cref="XmlDocReader.GetExample"/> returns <c>null</c> when the example element contains only whitespace.</summary>
    [Fact]
    public void XmlDocReader_GetExample_WhitespaceOnly_ReturnsNull()
    {
        // Arrange: create a member whose <example> element holds only whitespace
        var path = WriteXmlDoc("""
            <member name="M:Foo.Bar.Sample">
              <example>   </example>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);

            // Assert: whitespace-only content must collapse to null, not an empty string
            Assert.Null(reader.GetExample("M:Foo.Bar.Sample"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetExampleParts"/> returns the whole text as a single
    ///     code part when the example has no <c>&lt;code&gt;</c> child element.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetExampleParts_NoCodeElement_ReturnsWholeTextAsCodePart()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:Foo.Bar.Sample">
              <example>var x = new Bar();</example>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var parts = reader.GetExampleParts("M:Foo.Bar.Sample");

            // Assert: single part, treated as code
            Assert.Single(parts);
            Assert.True(parts[0].IsCode);
            Assert.Equal("var x = new Bar();", parts[0].Content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetExampleParts"/> separates prose text nodes from
    ///     <c>&lt;code&gt;</c> child elements when both are present.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetExampleParts_WithCodeElement_SeparatesProseFromCode()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:Foo.Bar.Sample">
              <example>Use this method like so:<code>var x = new Bar();
            x.Run();</code></example>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var parts = reader.GetExampleParts("M:Foo.Bar.Sample");

            // Assert: prose part followed by code part
            Assert.Equal(2, parts.Count);
            Assert.False(parts[0].IsCode);
            Assert.Equal("Use this method like so:", parts[0].Content);
            Assert.True(parts[1].IsCode);
            Assert.Equal("var x = new Bar();\nx.Run();", parts[1].Content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetExampleParts"/> returns an empty list when
    ///     the member identifier is not present in the XML doc file.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetExampleParts_MemberAbsent_ReturnsEmpty()
    {
        // Arrange
        var path = WriteXmlDoc(string.Empty);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var parts = reader.GetExampleParts("M:Missing.Type.Method");

            // Assert
            Assert.Empty(parts);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates that <see cref="XmlDocReader.GetExceptionDetails"/> returns formatted type names and descriptions.</summary>
    [Fact]
    public void XmlDocReader_GetExceptionDetails_MemberWithExceptions_ReturnsFormattedTypesAndDescriptions()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:Foo.Bar.Open(System.String)">
              <exception cref="T:System.InvalidOperationException">Already open.</exception>
              <exception cref="T:System.ArgumentNullException">host is null.</exception>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var details = reader.GetExceptionDetails("M:Foo.Bar.Open(System.String)");

            // Assert
            Assert.Equal(2, details.Count);
            Assert.Equal("InvalidOperationException", details[0].Type);
            Assert.Equal("Already open.", details[0].Description);
            Assert.Equal("ArgumentNullException", details[1].Type);
            Assert.Equal("host is null.", details[1].Description);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates that <see cref="XmlDocReader.GetRemarks"/> returns null when the member is absent.</summary>
    [Fact]
    public void XmlDocReader_GetRemarks_MemberAbsent_ReturnsNull()
    {
        // Arrange
        var path = WriteXmlDoc(string.Empty);
        try
        {
            // Act
            var reader = new XmlDocReader(path);

            // Assert
            Assert.Null(reader.GetRemarks("T:Missing.Type"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates that <see cref="XmlDocReader.GetReturns"/> returns null when the member is absent.</summary>
    [Fact]
    public void XmlDocReader_GetReturns_MemberAbsent_ReturnsNull()
    {
        // Arrange
        var path = WriteXmlDoc(string.Empty);
        try
        {
            // Act
            var reader = new XmlDocReader(path);

            // Assert
            Assert.Null(reader.GetReturns("T:Missing.Type"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates that <see cref="XmlDocReader.GetExample"/> returns null when the member is absent.</summary>
    [Fact]
    public void XmlDocReader_GetExample_MemberAbsent_ReturnsNull()
    {
        // Arrange
        var path = WriteXmlDoc(string.Empty);
        try
        {
            // Act
            var reader = new XmlDocReader(path);

            // Assert
            Assert.Null(reader.GetExample("T:Missing.Type"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
