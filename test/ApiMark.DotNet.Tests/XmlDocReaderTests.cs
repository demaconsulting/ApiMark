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

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetSummary"/> follows a <c>cref</c> inheritdoc reference
    ///     and returns the summary from the referenced target member.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetSummary_InheritDocWithCref_ReturnsSummaryFromTarget()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:MyNamespace.MyClass.MyMethod">
                <inheritdoc cref="M:MyNamespace.BaseClass.BaseMethod" />
            </member>
            <member name="M:MyNamespace.BaseClass.BaseMethod">
                <summary>Base summary text.</summary>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var summary = reader.GetSummary("M:MyNamespace.MyClass.MyMethod");

            // Assert
            Assert.Equal("Base summary text.", summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetRemarks"/> follows a <c>cref</c> inheritdoc reference
    ///     and returns the remarks from the referenced target member.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetRemarks_InheritDocWithCref_ReturnsRemarksFromTarget()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:MyNamespace.MyClass.MyMethod">
                <inheritdoc cref="M:MyNamespace.BaseClass.BaseMethod" />
            </member>
            <member name="M:MyNamespace.BaseClass.BaseMethod">
                <remarks>Base remarks text.</remarks>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var remarks = reader.GetRemarks("M:MyNamespace.MyClass.MyMethod");

            // Assert
            Assert.Equal("Base remarks text.", remarks);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetParams"/> follows a <c>cref</c> inheritdoc reference
    ///     and returns parameters from the referenced target member.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetParams_InheritDocWithCref_ReturnsParamsFromTarget()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:MyNamespace.MyClass.MyMethod(System.String)">
                <inheritdoc cref="M:MyNamespace.BaseClass.BaseMethod(System.String)" />
            </member>
            <member name="M:MyNamespace.BaseClass.BaseMethod(System.String)">
                <param name="input">The input value.</param>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var parameters = reader.GetParams("M:MyNamespace.MyClass.MyMethod(System.String)");

            // Assert
            Assert.Single(parameters);
            Assert.Equal("input", parameters[0].Name);
            Assert.Equal("The input value.", parameters[0].Description);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetReturns"/> follows a <c>cref</c> inheritdoc reference
    ///     and returns the returns text from the referenced target member.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetReturns_InheritDocWithCref_ReturnsReturnsFromTarget()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:MyNamespace.MyClass.MyMethod">
                <inheritdoc cref="M:MyNamespace.BaseClass.BaseMethod" />
            </member>
            <member name="M:MyNamespace.BaseClass.BaseMethod">
                <returns>The computed result.</returns>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var returns = reader.GetReturns("M:MyNamespace.MyClass.MyMethod");

            // Assert
            Assert.Equal("The computed result.", returns);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetSummary"/> returns <c>null</c> when the
    ///     <c>cref</c> target referenced by <c>&lt;inheritdoc /&gt;</c> does not exist in the XML doc file.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetSummary_InheritDocWithCref_MissingTarget_ReturnsNull()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:MyNamespace.MyClass.MyMethod">
                <inheritdoc cref="M:MyNamespace.BaseClass.MissingMethod" />
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var summary = reader.GetSummary("M:MyNamespace.MyClass.MyMethod");

            // Assert: missing cref target must degrade gracefully to null
            Assert.Null(summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetSummary"/> returns <c>null</c> rather than
    ///     throwing when a cyclic <c>&lt;inheritdoc cref="..." /&gt;</c> chain is encountered.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetSummary_InheritDocWithCref_CyclicReference_ReturnsNull()
    {
        // Arrange: A -> B -> A forms a cycle
        var path = WriteXmlDoc("""
            <member name="M:MyNamespace.A.Method">
                <inheritdoc cref="M:MyNamespace.B.Method" />
            </member>
            <member name="M:MyNamespace.B.Method">
                <inheritdoc cref="M:MyNamespace.A.Method" />
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var summary = reader.GetSummary("M:MyNamespace.A.Method");

            // Assert: cycle must degrade gracefully to null without throwing
            Assert.Null(summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetSummary"/> applies the <c>path</c> XPath
    ///     filter from <c>&lt;inheritdoc /&gt;</c> and returns only the matching section.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetSummary_InheritDocWithPath_ReturnsFilteredSummary()
    {
        // Arrange: cref points to a member with both summary and remarks; path selects only summary
        var path = WriteXmlDoc("""
            <member name="M:MyNamespace.MyClass.MyMethod">
                <inheritdoc cref="M:MyNamespace.BaseClass.BaseMethod" path="//summary" />
            </member>
            <member name="M:MyNamespace.BaseClass.BaseMethod">
                <summary>Filtered summary text.</summary>
                <remarks>These remarks must not appear.</remarks>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var summary = reader.GetSummary("M:MyNamespace.MyClass.MyMethod");

            // Assert: path filter must select only the summary element
            Assert.Equal("Filtered summary text.", summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetSummary"/> returns <c>null</c> when the
    ///     <c>path</c> XPath expression does not match any node in the resolved source member.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetSummary_InheritDocWithPath_NonMatchingPath_ReturnsNull()
    {
        // Arrange: source has summary, but path expression selects a non-existent element
        var path = WriteXmlDoc("""
            <member name="M:MyNamespace.MyClass.MyMethod">
                <inheritdoc cref="M:MyNamespace.BaseClass.BaseMethod" path="//nonexistent" />
            </member>
            <member name="M:MyNamespace.BaseClass.BaseMethod">
                <summary>A summary.</summary>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var summary = reader.GetSummary("M:MyNamespace.MyClass.MyMethod");

            // Assert: non-matching path must degrade gracefully to null
            Assert.Null(summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetSummary"/> resolves an explicit <c>cref</c>
    ///     target and applies a <c>path</c> filter to return only the matching section.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetSummary_InheritDocWithCrefAndPath_ReturnsFilteredSummaryFromTarget()
    {
        // Arrange: cref selects an explicit target; path restricts to the summary element only
        var path = WriteXmlDoc("""
            <member name="M:MyNamespace.MyClass.MyMethod">
                <inheritdoc cref="M:MyNamespace.OtherClass.OtherMethod" path="//summary" />
            </member>
            <member name="M:MyNamespace.OtherClass.OtherMethod">
                <summary>Other summary text.</summary>
                <remarks>Other remarks text.</remarks>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var summary = reader.GetSummary("M:MyNamespace.MyClass.MyMethod");

            // Assert: cref + path must select only the summary from the named target
            Assert.Equal("Other summary text.", summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetSummary"/> resolves a bare
    ///     <c>&lt;inheritdoc /&gt;</c> (no cref) using an injected inheritance chain and returns
    ///     the summary from the base member.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetSummary_InheritDocBare_WithChain_ReturnsSummaryFromBase()
    {
        // Arrange: XML doc has bare inheritdoc; chain maps derived -> base
        var path = WriteXmlDoc("""
            <member name="M:MyNamespace.MyClass.MyMethod">
                <inheritdoc />
            </member>
            <member name="M:MyNamespace.BaseClass.BaseMethod">
                <summary>Base summary text.</summary>
            </member>
            """);
        var chain = new Dictionary<string, IReadOnlyList<string>>
        {
            ["M:MyNamespace.MyClass.MyMethod"] = new List<string> { "M:MyNamespace.BaseClass.BaseMethod" },
        };
        try
        {
            // Act
            var reader = new XmlDocReader(path, chain);
            var summary = reader.GetSummary("M:MyNamespace.MyClass.MyMethod");

            // Assert: bare inheritdoc must follow the chain to the base member
            Assert.Equal("Base summary text.", summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetSummary"/> returns <c>null</c> for a bare
    ///     <c>&lt;inheritdoc /&gt;</c> when no inheritance chain is provided.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetSummary_InheritDocBare_NoChain_ReturnsNull()
    {
        // Arrange: bare inheritdoc with no chain injected
        var path = WriteXmlDoc("""
            <member name="M:MyNamespace.MyClass.MyMethod">
                <inheritdoc />
            </member>
            """);
        try
        {
            // Act: construct without an inheritance chain
            var reader = new XmlDocReader(path);
            var summary = reader.GetSummary("M:MyNamespace.MyClass.MyMethod");

            // Assert: without a chain, bare inheritdoc must degrade gracefully to null
            Assert.Null(summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetSummary"/> returns <c>null</c> for a bare
    ///     <c>&lt;inheritdoc /&gt;</c> when the chain entry's target member is absent from the XML doc file.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetSummary_InheritDocBare_ChainMemberAbsent_ReturnsNull()
    {
        // Arrange: chain points to a member not present in the XML doc file
        var path = WriteXmlDoc("""
            <member name="M:MyNamespace.MyClass.MyMethod">
                <inheritdoc />
            </member>
            """);
        var chain = new Dictionary<string, IReadOnlyList<string>>
        {
            ["M:MyNamespace.MyClass.MyMethod"] = new List<string> { "M:MyNamespace.BaseClass.MissingMethod" },
        };
        try
        {
            // Act
            var reader = new XmlDocReader(path, chain);
            var summary = reader.GetSummary("M:MyNamespace.MyClass.MyMethod");

            // Assert: absent chain target must degrade gracefully to null
            Assert.Null(summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetSummary"/> resolves a multi-hop
    ///     <c>&lt;inheritdoc cref="..." /&gt;</c> chain transitively (A inherits from B, B inherits from C).
    /// </summary>
    [Fact]
    public void XmlDocReader_GetSummary_InheritDocChained_ResolvesTransitively()
    {
        // Arrange: A -> B via cref, B -> C via cref; summary is on C
        var path = WriteXmlDoc("""
            <member name="M:MyNamespace.A.Method">
                <inheritdoc cref="M:MyNamespace.B.Method" />
            </member>
            <member name="M:MyNamespace.B.Method">
                <inheritdoc cref="M:MyNamespace.C.Method" />
            </member>
            <member name="M:MyNamespace.C.Method">
                <summary>Transitive summary text.</summary>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var summary = reader.GetSummary("M:MyNamespace.A.Method");

            // Assert: multi-hop chain must resolve transitively
            Assert.Equal("Transitive summary text.", summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetExceptions"/> follows a <c>cref</c>
    ///     inheritdoc reference and returns exceptions from the referenced target member.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetExceptions_InheritDocWithCref_ReturnsExceptionsFromTarget()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:TestClass.Method">
                <inheritdoc cref="M:TargetClass.Method" />
            </member>
            <member name="M:TargetClass.Method">
                <exception cref="T:System.ArgumentNullException">Null arg</exception>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var exceptions = reader.GetExceptions("M:TestClass.Method");

            // Assert: exceptions must be inherited from the cref target
            Assert.Single(exceptions);
            Assert.Contains("T:System.ArgumentNullException", exceptions);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetExceptionDetails"/> follows a <c>cref</c>
    ///     inheritdoc reference and returns exception details from the referenced target member.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetExceptionDetails_InheritDocWithCref_ReturnsExceptionDetailsFromTarget()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:TestClass.Method">
                <inheritdoc cref="M:TargetClass.Method" />
            </member>
            <member name="M:TargetClass.Method">
                <exception cref="T:System.ArgumentNullException">Null arg</exception>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var details = reader.GetExceptionDetails("M:TestClass.Method");

            // Assert: exception details must be inherited from the cref target
            Assert.Single(details);
            Assert.Equal("ArgumentNullException", details[0].Type);
            Assert.Equal("Null arg", details[0].Description);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetExample"/> follows a <c>cref</c>
    ///     inheritdoc reference and returns the example from the referenced target member.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetExample_InheritDocWithCref_ReturnsExampleFromTarget()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:TestClass.Method">
                <inheritdoc cref="M:TargetClass.Method" />
            </member>
            <member name="M:TargetClass.Method">
                <example>Example text</example>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var example = reader.GetExample("M:TestClass.Method");

            // Assert: example must be inherited from the cref target
            Assert.Equal("Example text", example);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that <see cref="XmlDocReader.GetExampleParts"/> follows a <c>cref</c>
    ///     inheritdoc reference and returns example parts from the referenced target member.
    /// </summary>
    [Fact]
    public void XmlDocReader_GetExampleParts_InheritDocWithCref_ReturnsExamplePartsFromTarget()
    {
        // Arrange
        var path = WriteXmlDoc("""
            <member name="M:TestClass.Method">
                <inheritdoc cref="M:TargetClass.Method" />
            </member>
            <member name="M:TargetClass.Method">
                <example>Example text</example>
            </member>
            """);
        try
        {
            // Act
            var reader = new XmlDocReader(path);
            var parts = reader.GetExampleParts("M:TestClass.Method");

            // Assert: example parts must be inherited from the cref target
            Assert.Single(parts);
            Assert.True(parts[0].IsCode);
            Assert.Equal("Example text", parts[0].Content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Regression test for the branch-local visited-set fix: verifies that when a bare
    ///     <c>&lt;inheritdoc /&gt;</c> has multiple chain candidates, a failed traversal
    ///     of the first candidate does not poison the visited set seen by the second candidate.
    ///     <para>
    ///         Setup: <c>A.Method</c> has a bare inheritdoc with chain <c>[B.Method, C.Method]</c>.
    ///         <c>B.Method</c> has <c>&lt;inheritdoc cref="C.Method" /&gt;</c> (explicit cref to C).
    ///         <c>C.Method</c> has a direct <c>&lt;summary&gt;</c>.
    ///         When A tries B, B resolves to C and succeeds; A returns C's summary via B.
    ///         The fix ensures that even if B's traversal had visited C and failed, A's fresh
    ///         branch-local visited set would still allow A to try C directly.
    ///     </para>
    /// </summary>
    [Fact]
    public void XmlDocReader_GetSummary_InheritDocBare_MultipleChainCandidates_SecondCandidateNotBlockedByFirstsVisited()
    {
        // Arrange: A bare inheritdoc, chain [B, C]; B crefs to C; C has direct summary
        var path = WriteXmlDoc("""
            <member name="M:A.Method">
                <inheritdoc />
            </member>
            <member name="M:B.Method">
                <inheritdoc cref="M:C.Method" />
            </member>
            <member name="M:C.Method">
                <summary>C direct summary.</summary>
            </member>
            """);
        var chain = new Dictionary<string, IReadOnlyList<string>>
        {
            ["M:A.Method"] = new List<string> { "M:B.Method", "M:C.Method" },
        };
        try
        {
            // Act
            var reader = new XmlDocReader(path, chain);
            var summary = reader.GetSummary("M:A.Method");

            // Assert: A must resolve to C's summary (either directly through B's cref, or
            // through A's own second candidate C) — the visited set from B's traversal
            // must not block A from trying C independently
            Assert.Equal("C direct summary.", summary);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
