using ApiMark.Core;
using ApiMark.Vhdl.VhdlAst;

namespace ApiMark.Vhdl;

/// <summary>
///     Scans the file models parsed by <see cref="VhdlGenerator"/> for entities, ports,
///     generics, packages, and package-level exported declarations that lack a documentation
///     summary.
/// </summary>
/// <remarks>
///     <para>
///         <b>Public-interface-only scope (v1 simplification).</b> VHDL has no accessibility
///         concept analogous to C#/C++ visibility modifiers, so this checker has no visibility
///         tier parameter at all — it always checks the same set of declarations regardless of
///         the caller-supplied <c>--enforce-docs</c> value (<c>Public</c>,
///         <c>PublicAndProtected</c>, and <c>All</c> are all accepted by
///         <see cref="VhdlGenerator.CheckDocumentationCoverage"/> purely for CLI vocabulary
///         consistency with the .NET and C++ subcommands, then discarded).
///     </para>
///     <para>
///         <b>Architecture internals are not checked.</b> <see cref="VhdlArchitectureDecl"/>
///         carries only its own <see cref="VhdlDocComment"/> — internal signals, variables, and
///         processes are not parsed into the AST model today, so there is nothing for this
///         checker to walk beyond the architecture declaration itself. This checker DOES check
///         the architecture's own summary (an architecture is a named, referenceable
///         declaration analogous to a class), but explicitly does NOT check anything inside it.
///         Enforcing documentation on architecture-internal signals/processes is a deferred
///         future enhancement, not a bug — see the VHDL design documentation.
///     </para>
///     <para>
///         The v1 bar for "documented" mirrors .NET/C++: a declaration is considered documented
///         when its <see cref="VhdlDocComment.Summary"/> is a non-null, non-whitespace string.
///     </para>
/// </remarks>
internal static class DocumentationCoverageChecker
{
    /// <summary>
    ///     Scans <paramref name="fileModels"/> for entities (and their generics/ports),
    ///     architectures, and packages (and their types/constants/components/subprograms) that
    ///     lack a non-empty documentation summary.
    /// </summary>
    /// <param name="fileModels">
    ///     The file models collected by <see cref="VhdlGenerator.Parse"/>. May contain fewer
    ///     entries than the number of source files scanned, since <c>Parse</c> tolerates and
    ///     logs per-file parse failures without adding a model for the failed file.
    /// </param>
    /// <returns>
    ///     A <see cref="DocumentationCoverageResult"/> describing every undocumented declaration
    ///     found and the total number of declarations checked.
    /// </returns>
    internal static DocumentationCoverageResult Check(IReadOnlyList<VhdlFileModel> fileModels)
    {
        var undocumented = new List<UndocumentedApiItem>();
        var checkedCount = 0;

        foreach (var file in fileModels)
        {
            foreach (var entity in file.Entities)
            {
                CheckEntity(entity, undocumented, ref checkedCount);
            }

            // Architecture bodies are named, referenceable declarations in their own right, so
            // their own summary is checked — but their internals (signals/processes) are not
            // parsed into the AST model today, so there is nothing further to walk here. See the
            // remarks on this class for the deferred-enhancement rationale.
            foreach (var architecture in file.Architectures)
            {
                checkedCount++;
                if (string.IsNullOrWhiteSpace(architecture.Doc?.Summary))
                {
                    undocumented.Add(new UndocumentedApiItem("Architecture", $"{architecture.EntityName}({architecture.Name})"));
                }
            }

            foreach (var package in file.Packages)
            {
                CheckPackage(package, undocumented, ref checkedCount);
            }
        }

        return new DocumentationCoverageResult(undocumented, checkedCount);
    }

    /// <summary>Checks a single entity, and each of its generics and ports, for a missing summary.</summary>
    /// <param name="entity">The entity to check.</param>
    /// <param name="undocumented">The accumulator list to append violations to.</param>
    /// <param name="checkedCount">Running count of declarations checked, incremented by reference.</param>
    private static void CheckEntity(VhdlEntityDecl entity, List<UndocumentedApiItem> undocumented, ref int checkedCount)
    {
        checkedCount++;
        if (string.IsNullOrWhiteSpace(entity.Doc?.Summary))
        {
            undocumented.Add(new UndocumentedApiItem("Entity", entity.Name));
        }

        foreach (var generic in entity.Generics)
        {
            checkedCount++;
            if (string.IsNullOrWhiteSpace(generic.Doc?.Summary))
            {
                undocumented.Add(new UndocumentedApiItem("Generic", $"{entity.Name}.{generic.Name}"));
            }
        }

        foreach (var port in entity.Ports)
        {
            checkedCount++;
            if (string.IsNullOrWhiteSpace(port.Doc?.Summary))
            {
                undocumented.Add(new UndocumentedApiItem("Port", $"{entity.Name}.{port.Name}"));
            }
        }
    }

    /// <summary>
    ///     Checks a single package, and each of its types, constants, components, and
    ///     subprograms, for a missing summary.
    /// </summary>
    /// <param name="package">The package to check.</param>
    /// <param name="undocumented">The accumulator list to append violations to.</param>
    /// <param name="checkedCount">Running count of declarations checked, incremented by reference.</param>
    private static void CheckPackage(VhdlPackageDecl package, List<UndocumentedApiItem> undocumented, ref int checkedCount)
    {
        checkedCount++;
        if (string.IsNullOrWhiteSpace(package.Doc?.Summary))
        {
            undocumented.Add(new UndocumentedApiItem("Package", package.Name));
        }

        foreach (var type in package.Types)
        {
            checkedCount++;
            if (string.IsNullOrWhiteSpace(type.Doc?.Summary))
            {
                undocumented.Add(new UndocumentedApiItem("Type", $"{package.Name}.{type.Name}"));
            }
        }

        foreach (var constant in package.Constants)
        {
            checkedCount++;
            if (string.IsNullOrWhiteSpace(constant.Doc?.Summary))
            {
                undocumented.Add(new UndocumentedApiItem("Constant", $"{package.Name}.{constant.Name}"));
            }
        }

        foreach (var component in package.Components)
        {
            checkedCount++;
            if (string.IsNullOrWhiteSpace(component.Doc?.Summary))
            {
                undocumented.Add(new UndocumentedApiItem("Component", $"{package.Name}.{component.Name}"));
            }
        }

        foreach (var subprogram in package.Subprograms)
        {
            checkedCount++;
            if (string.IsNullOrWhiteSpace(subprogram.Doc?.Summary))
            {
                undocumented.Add(new UndocumentedApiItem("Subprogram", $"{package.Name}.{subprogram.Name}"));
            }
        }
    }
}
