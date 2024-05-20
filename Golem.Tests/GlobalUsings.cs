global using Xunit;
global using Xunit.Abstractions;
using System.Threading;

[assembly: CollectionBehavior(
    CollectionBehavior.CollectionPerAssembly,
    DisableTestParallelization = true
)]

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
