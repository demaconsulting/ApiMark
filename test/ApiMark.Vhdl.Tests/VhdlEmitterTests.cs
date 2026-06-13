using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Vhdl.VhdlAst;
using Xunit;

namespace ApiMark.Vhdl.Tests;

/// <summary>Unit tests for <see cref="VhdlEmitter"/>.</summary>
public class VhdlEmitterTests
{
    /// <summary>Validates that passing null factory to Emit throws ArgumentNullException.</summary>
    [Fact]
    public void VhdlEmitter_Emit_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var emitter = new VhdlEmitter(options, []);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            emitter.Emit(null!, new EmitConfig(), new InMemoryContext()));
    }
}
