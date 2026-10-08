// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>
///     Bundles the context <see cref="XmlDocReader"/> needs to resolve a <c>&lt;see cref&gt;</c>/
///     <c>&lt;seealso cref&gt;</c> reference to a real relative Markdown link instead of its
///     default code-span-only fallback rendering.
/// </summary>
/// <remarks>
///     Every public <see cref="XmlDocReader"/> method accepts this as an optional, trailing,
///     nullable parameter (<see langword="null"/> by default). When <see langword="null"/>, or
///     when a given <c>cref</c> cannot be resolved to an intra-assembly symbol that will actually
///     be emitted, rendering falls back unchanged to today's code-span-or-plain-text behavior —
///     this is how the single-file emitter (<c>DotNetEmitterSingleFile</c>) opts out of linking
///     entirely, simply by never constructing or passing one.
///     <para>
///     Deliberately carries only primitives, delegates, and types that are already
///     <c>ApiMark.DotNet</c>-internal peers of <see cref="XmlDocReader"/> (<see cref="TypeLinkResolver"/>,
///     <see cref="CrefTargetResolver"/>) — not a <c>DotNetEmitter</c>/<c>DotNetAstModel</c>
///     reference — so that <see cref="XmlDocReader"/> does not take on a hard dependency on the
///     emitter/model layer merely to support cref linking.
///     </para>
/// </remarks>
/// <param name="Targets">
///     Resolves a raw <c>cref</c> identifier string to the type or member it names, when that
///     symbol is declared in the assembly being documented.
/// </param>
/// <param name="Resolver">
///     Builds the actual Markdown link once a <c>cref</c> target has been resolved and confirmed
///     to be emitted.
/// </param>
/// <param name="MemberPageIndex">
///     Map from a member's XML-doc identifier to its gradual-disclosure page key, consulted by
///     <see cref="TypeLinkResolver.LinkifyResolvedMember"/>.
/// </param>
/// <param name="IsTypeEmitted">
///     Returns <see langword="true"/> when the given type will actually be emitted as a page
///     under the active visibility/obsolete/exclude-pattern settings. Typically bound to the
///     active <c>DotNetEmitter</c>'s own visibility rules so that this logic is never duplicated.
/// </param>
/// <param name="IsMemberEmitted">
///     Returns <see langword="true"/> when the given member will actually be emitted. Typically
///     bound to <c>DotNetEmitter.ShouldIncludeMember</c>.
/// </param>
/// <param name="CurrentFolder">
///     Folder path of the Markdown file currently being rendered, relative to the documentation
///     output root — forwarded to <see cref="TypeLinkResolver.LinkifyResolvedType"/>/
///     <see cref="TypeLinkResolver.LinkifyResolvedMember"/> to compute the relative link path.
/// </param>
internal sealed record CrefLinkContext(
    CrefTargetResolver Targets,
    TypeLinkResolver Resolver,
    IReadOnlyDictionary<string, string> MemberPageIndex,
    Func<TypeDefinition, bool> IsTypeEmitted,
    Func<IMemberDefinition, bool> IsMemberEmitted,
    string CurrentFolder);
