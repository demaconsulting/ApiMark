namespace ApiMark.DotNet.Fixtures.Navigation;

/// <summary>Child namespace used to verify namespace-page breadcrumbs and api.md key types.</summary>
internal static class NamespaceDoc
{
}

/// <summary>The alphabetically last documented navigation type.</summary>
public class Delta
{
}

/// <summary>A documented navigation type.</summary>
public class Charlie
{
}

/// <summary>A documented navigation type.</summary>
public class Bravo
{
}

#pragma warning disable CS1591 // Intentionally undocumented: must rank after every documented type.

public class Alpha
{
}

public class Zulu
{
}

#pragma warning restore CS1591
