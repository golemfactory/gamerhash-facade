global using Xunit;
[assembly: CollectionBehavior(
    CollectionBehavior.CollectionPerClass,
    DisableTestParallelization = true,
    MaxParallelThreads = 0
)]

[CollectionDefinition(nameof(SerialTestCollection), DisableParallelization = true)]
public class SerialTestCollection { }

public class GolemFixture : IDisposable
{
    public GolemFixture(IMessageSink sink)
    {
        Sink = sink;
    }

    public IMessageSink Sink { get; }

    public void Dispose()
    {
    }
}
