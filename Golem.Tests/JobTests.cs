using System.Threading.Channels;

using App;

using Golem;
using Golem.IntegrationTests.Tools;
using Golem.Yagna;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Golem.Tests
{

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

    [Collection("Sequential")]
    public class JobTests : IDisposable, IAsyncLifetime, IClassFixture<GolemFixture>
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private GolemRelay? _relay;
        private GolemRequestor? _requestor;

        public JobTests(ITestOutputHelper outputHelper, GolemFixture golemFixture)
        {
            XunitContext.Register(outputHelper);
            var logfile = Path.Combine(PackageBuilder.TestDir(nameof(JobTests)), "gh_facade-{Date}.log");
            var loggerProvider = new TestLoggerProvider(golemFixture.Sink);
            _logger = loggerProvider.CreateLogger(nameof(JobTests));
            _loggerFactory = LoggerFactory.Create(builder => builder
                .AddSimpleConsole(options => options.SingleLine = true)
                .AddFile(logfile)
                .AddProvider(loggerProvider)
            );
        }

        public async Task InitializeAsync()
        {
            _relay = await GolemRelay.Build(nameof(JobTests), _loggerFactory.CreateLogger("Relay"));
            Assert.True(_relay.Start());
            System.Environment.SetEnvironmentVariable("YA_NET_RELAY_HOST", "127.0.0.1:17464");
            System.Environment.SetEnvironmentVariable("RUST_LOG", "debug");

            _requestor = await GolemRequestor.Build(nameof(JobTests), _loggerFactory.CreateLogger("Requestor"), false);
            Assert.True(_requestor.Start());
            _requestor.InitAccount();
        }

        [Fact]
        public async Task StartStop_Job()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory(nameof(JobTests));
            _logger.LogInformation($"Path: {golemPath}");
            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), _loggerFactory);

            var statusChannel = Channel.CreateUnbounded<GolemStatus>();
            Action<GolemStatus> golemStatus = async (v) =>
            {
                _logger.LogInformation($"Golem status update. {v}");
                await statusChannel.Writer.WriteAsync(v);
            };
            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), golemStatus).Subscribe();

            var jobChannel = Channel.CreateUnbounded<IJob?>();
            Action<IJob?> currentJobHook = async (v) =>
            {
                _logger.LogInformation($"Current Job update. {v}");
                await jobChannel.Writer.WriteAsync(v);
            };
            golem.PropertyChanged += new PropertyChangedHandler<Golem, IJob?>(nameof(IGolem.CurrentJob), currentJobHook).Subscribe();

            _logger.LogInformation("Starting Golem");
            await golem.Start();
            Assert.Equal(GolemStatus.Starting, await statusChannel.Reader.ReadAsync());
            GolemStatus? status = GolemStatus.Starting;
            while ((status = await statusChannel.Reader.ReadAsync()) == GolemStatus.Starting)
            {
                _logger.LogInformation("Still starting");
            }
            Assert.Equal(status, GolemStatus.Ready);

            Assert.Null(golem.CurrentJob);

            _logger.LogInformation("Starting App");
            var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            var jobStartCounter = 0;
            IJob? job = null;
            while ((job = await jobChannel.Reader.ReadAsync()) == null && jobStartCounter++ < 100)
            {
                _logger.LogInformation("Still no job");
            }
            _logger.LogInformation($"Got a job. Status {golem.CurrentJob?.Status}, Id: {golem.CurrentJob?.Id}, RequestorId: {golem.CurrentJob?.RequestorId}");
            
            Assert.NotNull(golem.CurrentJob);
            Assert.Equal(golem.CurrentJob.RequestorId, _requestor?.AppKey?.Id);
            Assert.Equal(golem.CurrentJob?.Status, JobStatus.Idle);

            _logger.LogInformation("Stopping App");
            await app.Stop(StopMethod.SigInt);

            var jobStopCounter = 0;
            while ((job = await jobChannel.Reader.ReadAsync()) != null && jobStopCounter++ < 100)
            {
                _logger.LogInformation($"Still has a job. Status: {job.Status}, Id: {job.Id}, RequestorId: {job.RequestorId}");
            }
            _logger.LogInformation("No more jobs");
            Assert.Null(golem.CurrentJob);

            _logger.LogInformation("Stopping Golem");
            await golem.Stop();

            var golemStopCounter = 0;
            while ((status = await statusChannel.Reader.ReadAsync()) == GolemStatus.Ready && golemStopCounter++ < 100)
            {
                _logger.LogInformation("Still stopping");
            }
            Assert.Equal(status, GolemStatus.Off);
        }

        public async Task DisposeAsync()
        {
            if (_requestor != null)
            {
                await _requestor.Stop(StopMethod.SigInt);
            }
            if (_relay != null)
            {
                await _relay.Stop(StopMethod.SigInt);
            }
        }

        public void Dispose()
        {
            XunitContext.Flush();
        }
    }
}
