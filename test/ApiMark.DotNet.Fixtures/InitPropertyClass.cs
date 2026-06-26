// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A class with an init-only property for testing C# 9 init accessor detection.</summary>
/// <remarks>
///     Used by <c>DotNetEmitter_BuildPropertyAccessors_InitOnlySetter_EmitsInit</c> to verify
///     that <c>BuildPropertyAccessors</c> emits the <c>init</c> keyword for init-only setters.
/// </remarks>
public class InitPropertyClass
{
    /// <summary>Gets or initializes the name.</summary>
    public string InitOnlyProperty { get; init; } = string.Empty;
}
