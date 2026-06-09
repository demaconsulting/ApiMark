namespace ApiMark.DotNet.Fixtures;

/// <summary>A sample delegate that reports a service event.</summary>
public delegate void ServiceEvent(DateTime timestamp, string service, string name, object[] arguments);

/// <summary>A sample generic delegate that transforms a value.</summary>
public delegate TResult SampleTransform<TInput, TResult>(TInput input);
