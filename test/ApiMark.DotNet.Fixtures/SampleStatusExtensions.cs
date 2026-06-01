namespace ApiMark.DotNet.Fixtures;

/// <summary>Extensions for the <see cref="SampleStatus" /> enum.</summary>
public static class SampleStatusExtensions
{
    /// <summary>Returns <see langword="true" /> when <paramref name="status" /> is <see cref="SampleStatus.Active" /> or <see cref="SampleStatus.Pending" />.</summary>
    public static bool IsPassed(this SampleStatus status) => status is SampleStatus.Active or SampleStatus.Pending;

    /// <summary>Returns <see langword="true" /> when <paramref name="status" /> is <see cref="SampleStatus.Active" /> or, when <paramref name="includePending" /> is <see langword="true" />, <see cref="SampleStatus.Pending" />.</summary>
    public static bool IsPassed(this SampleStatus status, bool includePending) =>
        status == SampleStatus.Active || (includePending && status == SampleStatus.Pending);
}
