// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>
///     A class declaring a documented field-like event, used to verify <c>--enforce-docs</c> and
///     Markdown emission do not flag or emit a separate page for the compiler-generated backing
///     field the C# compiler synthesizes for it. Unlike an auto-property's backing field, a
///     field-like event's backing field shares the event's exact name (no angle brackets), so it
///     is only distinguishable as compiler-generated via <c>CompilerGeneratedAttribute</c>.
/// </summary>
public sealed class FieldLikeEventClass
{
    /// <summary>A documented field-like event whose compiler-generated backing field shares its name.</summary>
    public event EventHandler? Updated;

    /// <summary>Raises <see cref="Updated"/>.</summary>
    public void RaiseUpdated() => Updated?.Invoke(this, EventArgs.Empty);
}
