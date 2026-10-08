// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>
///     Resolves a raw XML-doc <c>cref</c> attribute string to the Mono.Cecil type or member it
///     names, when that symbol is declared in a single, specific assembly.
/// </summary>
/// <remarks>
///     Built once per assembly by walking every type and member and computing its XML-doc
///     identifier via <see cref="DotNetEmitter.BuildTypeId"/>/<see cref="DotNetEmitter.BuildMemberId"/>
///     — the exact same identifier format the C# compiler emits for a <c>&lt;member name="..."&gt;</c>
///     entry in the XML documentation file, and therefore byte-identical to what appears verbatim
///     in a <c>cref="..."</c> attribute for an intra-assembly target. No new identifier-formatting
///     or parsing logic is introduced; this class is purely a reverse index over the builders that
///     already exist.
///     <para>
///     This resolver intentionally applies no visibility, obsolete, or exclude-pattern filtering —
///     it answers only "does a symbol with this XML-doc-ID string exist in this assembly", so that
///     a caller can distinguish "no such symbol" (external assembly, or a malformed/misspelled
///     cref) from "the symbol exists but is filtered out of the generated documentation", which
///     requires a separate, caller-supplied visibility check (see <c>CrefLinkContext</c>).
///     </para>
///     <para>
///     On duplicate XML-doc-ID collisions (which should not occur for well-formed assemblies, but
///     are tolerated defensively the same way <see cref="XmlDocReader"/> tolerates duplicate
///     member IDs in the XML documentation file), the first occurrence encountered while walking
///     <see cref="ModuleDefinition.GetTypes"/> wins and later duplicates are silently discarded.
///     </para>
/// </remarks>
internal sealed class CrefTargetResolver
{
    /// <summary>Index of type definitions keyed by their XML doc type identifier (e.g. <c>T:Namespace.Type</c>).</summary>
    private readonly Dictionary<string, TypeDefinition> _types = new(StringComparer.Ordinal);

    /// <summary>
    ///     Index of member definitions keyed by their XML doc member identifier
    ///     (e.g. <c>M:Namespace.Type.Method</c>, <c>P:Namespace.Type.Property</c>).
    /// </summary>
    private readonly Dictionary<string, IMemberDefinition> _members = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new <see cref="CrefTargetResolver"/> by walking every type (including
    ///     nested types) and member declared in <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">The assembly whose types and members to index.</param>
    internal CrefTargetResolver(AssemblyDefinition assembly)
    {
        // ModuleDefinition.GetTypes() is Mono.Cecil's flattening enumerator — it already
        // includes nested types at every depth, so no separate recursive walk is needed.
        foreach (var type in assembly.MainModule.GetTypes())
        {
            var typeId = DotNetEmitter.BuildTypeId(type);
            _types.TryAdd(typeId, type);

            foreach (var member in GetMembersForIndexing(type))
            {
                var memberId = DotNetEmitter.BuildMemberId(member);

                // BuildMemberId returns string.Empty for member kinds it does not recognize —
                // skip those rather than indexing a collision-prone empty key.
                if (memberId.Length > 0)
                {
                    _members.TryAdd(memberId, member);
                }
            }
        }
    }

    /// <summary>
    ///     Attempts to resolve <paramref name="crefId"/> to a type declared in the indexed
    ///     assembly.
    /// </summary>
    /// <param name="crefId">
    ///     The raw <c>cref</c> attribute value, including its <c>T:</c> kind prefix (e.g.
    ///     <c>T:MyNamespace.MyClass</c>).
    /// </param>
    /// <param name="type">
    ///     When this method returns <see langword="true"/>, the resolved type; otherwise
    ///     <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> when a type with this exact identifier was indexed.</returns>
    internal bool TryResolveType(string crefId, out TypeDefinition type) =>
        _types.TryGetValue(crefId, out type!);

    /// <summary>
    ///     Attempts to resolve <paramref name="crefId"/> to a member declared in the indexed
    ///     assembly.
    /// </summary>
    /// <param name="crefId">
    ///     The raw <c>cref</c> attribute value, including its <c>M:</c>/<c>P:</c>/<c>F:</c>/<c>E:</c>
    ///     kind prefix (e.g. <c>M:MyNamespace.MyClass.DoWork(System.Int32)</c>).
    /// </param>
    /// <param name="member">
    ///     When this method returns <see langword="true"/>, the resolved member; otherwise
    ///     <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> when a member with this exact identifier was indexed.</returns>
    internal bool TryResolveMember(string crefId, out IMemberDefinition member) =>
        _members.TryGetValue(crefId, out member!);

    /// <summary>
    ///     Enumerates every method, property, field, and event declared directly on
    ///     <paramref name="type"/>, for indexing purposes only (no visibility filtering).
    /// </summary>
    /// <param name="type">The type whose members to enumerate.</param>
    /// <returns>All members declared directly on <paramref name="type"/>.</returns>
    private static IEnumerable<IMemberDefinition> GetMembersForIndexing(TypeDefinition type)
    {
        foreach (var method in type.Methods)
        {
            yield return method;
        }

        foreach (var property in type.Properties)
        {
            yield return property;
        }

        foreach (var field in type.Fields)
        {
            yield return field;
        }

        foreach (var evt in type.Events)
        {
            yield return evt;
        }
    }
}
