namespace ApiMark.Core;

/// <summary>
///     Contract every language-specific generator must implement.
/// </summary>
/// <remarks>
///     <para>
///         Decouples callers (ApiMarkTask and Program) from any concrete language module.
///         A caller constructs a generator with language-specific options, then calls
///         <see cref="Parse"/> to obtain an <see cref="IApiEmitter"/>, which is then
///         invoked with an <see cref="EmitConfig"/> to produce the Markdown output.
///     </para>
///     <para>
///         The two-stage design separates I/O-heavy parsing (reading assemblies or C++
///         headers) from format-specific writing so that the parsed symbol data can be
///         emitted in different output formats without re-parsing.
///     </para>
/// </remarks>
public interface IApiGenerator
{
    /// <summary>
    ///     Parses the configured software component and returns an emitter ready to
    ///     produce Markdown documentation in the requested format.
    /// </summary>
    /// <param name="context">
    ///     Output channel used to emit informational and error messages during parsing.
    ///     Must not be null. Implementations use <see cref="IContext.WriteLine"/> for
    ///     informational output and <see cref="IContext.WriteError"/> for error or
    ///     warning messages.
    /// </param>
    /// <returns>
    ///     An <see cref="IApiEmitter"/> holding all data required to emit documentation
    ///     in any supported <see cref="OutputFormat"/>. The caller must subsequently
    ///     invoke <see cref="IApiEmitter.Emit"/> to write output.
    /// </returns>
    IApiEmitter Parse(IContext context);
}
