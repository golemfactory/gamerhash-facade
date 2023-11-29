using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Microsoft.Extensions.Options;
using Xunit.Sdk;


namespace Golem.IntegrationTests.Tools
{

    public sealed class TestLogger : ILogger
    {
        private readonly string _name;
        private readonly IMessageSink _msgSink;

        public TestLogger(
            string name,
            Func<IMessageSink> getMsgSink)
        {
            _name = name;
            _msgSink = getMsgSink();
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default!;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }
            var msg = new DiagnosticMessage($"[{eventId.Id,2}: {logLevel,-12}: {_name}] {formatter(state, exception)}");
            _msgSink.OnMessage(msg);
        }
    }

    [ProviderAlias("Test")]
    public sealed class TestLoggerProvider : ILoggerProvider
    {
        private readonly IMessageSink _msgSink;

        public TestLoggerProvider(IMessageSink msgSink) => (_msgSink) = (msgSink);

        public ILogger CreateLogger(string categoryName) =>
            new TestLogger(categoryName, () => _msgSink);

        public void Dispose()
        {
        }
    }

}
