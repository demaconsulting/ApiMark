// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>
///     A sealed record used to verify <c>--enforce-docs</c> and Markdown emission do not flag or
///     emit a page for the compiler-synthesized <c>EqualityContract</c> property, which a sealed
///     record declares as <see langword="private"/> and therefore only appears at the
///     <c>All</c> enforcement/emission visibility tier.
/// </summary>
/// <param name="Name">The record's name.</param>
public sealed record SealedRecordClass(string Name);

/// <summary>
///     A non-sealed public record used to verify <c>--enforce-docs</c> and Markdown emission do
///     not flag or emit a page for the compiler-synthesized <c>EqualityContract</c> property,
///     which a non-sealed record declares as <see langword="protected virtual"/> and therefore
///     is visible at the <c>PublicAndProtected</c> visibility tier.
/// </summary>
/// <param name="Name">The record's name.</param>
public record BaseRecordClass(string Name);

/// <summary>
///     A record derived from <see cref="BaseRecordClass"/>, whose compiler-synthesized
///     <c>EqualityContract</c> property is declared <see langword="protected override"/> — used
///     to verify derived records are also exempted from <c>--enforce-docs</c> and Markdown
///     emission for this member.
/// </summary>
/// <param name="Name">The record's name.</param>
/// <param name="Extra">An additional value.</param>
public record DerivedRecordClass(string Name, int Extra) : BaseRecordClass(Name);

/// <summary>
///     A hand-written class with a property named <c>EqualityContract</c> that is not
///     compiler-generated — used to verify the <c>EqualityContract</c> exemption is scoped to
///     compiler-generated members and does not suppress a genuinely undocumented, hand-written
///     property sharing the same name.
/// </summary>
public sealed class HandWrittenEqualityContractClass
{
#pragma warning disable CS1591 // Missing XML comment — intentional fixture for an undocumented property
    public Type? EqualityContract { get; }
#pragma warning restore CS1591
}
