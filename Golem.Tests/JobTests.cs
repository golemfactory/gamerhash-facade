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

namespace Golem.Tests
{

    [Collection("Sequential")]
    public class JobTests : IDisposable, IAsyncLifetime
    {
        private readonly ILoggerFactory _loggerFactory;
        private GolemRelay? _relay;
        private GolemRequestor? _requestor;

        public JobTests(ITestOutputHelper outputHelper)
        {
            XunitContext.Register(outputHelper);
            var logfile = Path.Combine(PackageBuilder.TestDir(nameof(JobTests)), "gh_facade-{Date}.log");
            _loggerFactory = LoggerFactory.Create(builder => builder
                .AddSimpleConsole(options => options.SingleLine = true)
                .AddFile(logfile)
            );
        }

        public async Task InitializeAsync()
        {
            _relay = await GolemRelay.Build(nameof(JobTests));
            Assert.True(_relay.Start());
            System.Environment.SetEnvironmentVariable("YA_NET_RELAY_HOST", "127.0.0.1:17464");
            System.Environment.SetEnvironmentVariable("RUST_LOG", "debug");

            _requestor = await GolemRequestor.Build(nameof(JobTests));
            Assert.True(_requestor.Start());
            _requestor.InitAccount();
        }

        [Fact]
        public async Task StartStop_Job()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory(nameof(JobTests));
            Console.WriteLine("Path: " + golemPath);
            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), _loggerFactory);

            var statusChannel = Channel.CreateUnbounded<GolemStatus>();
            Action<GolemStatus> golemStatus = async (v) =>
            {
                Console.WriteLine("Golem status update. {0}", v);
                await statusChannel.Writer.WriteAsync(v);
            };
            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), golemStatus).Subscribe();

            var jobChannel = Channel.CreateUnbounded<IJob>();
            Action<IJob?> currentJobHook = async (v) =>
            {
                Console.WriteLine("Current Job update. {0}", v);
                await jobChannel.Writer.WriteAsync(v);
            };
            golem.PropertyChanged += new PropertyChangedHandler<Golem, IJob?>(nameof(IGolem.CurrentJob), currentJobHook).Subscribe();

            Console.WriteLine("Starting Golem");
            await golem.Start();
            Assert.Equal(GolemStatus.Starting, await statusChannel.Reader.ReadAsync());
            GolemStatus? status = GolemStatus.Starting;
            while ((status = await statusChannel.Reader.ReadAsync()) == GolemStatus.Starting)
            {
                Console.WriteLine("Still starting");
            }
            Assert.Equal(status, GolemStatus.Ready);

            Assert.Null(golem.CurrentJob);

            Console.WriteLine("Starting App");
            var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            var jobStartCounter = 0;
            IJob? job = null;
            while ((job = await jobChannel.Reader.ReadAsync()) == null && jobStartCounter++ < 10)
            {
                Console.WriteLine("Still no job");
            }
            Console.WriteLine("Got a job. Status {0}, Id: {1}, RequestorId: {2}", golem.CurrentJob.Status, golem.CurrentJob.Id, golem.CurrentJob.RequestorId);
            Assert.NotNull(golem.CurrentJob);

            Console.WriteLine("Stopping App");
            await app.Stop(StopMethod.SigInt);

            var jobStopCounter = 0;
            while ((job = await jobChannel.Reader.ReadAsync()) != null && jobStopCounter++ < 10)
            {
                Console.WriteLine("Still has a job. Status: {0}, Id: {1}, RequestorId: {2}", job.Status, job.Id, job.RequestorId);
            }
            Assert.Null(golem.CurrentJob);

            Console.WriteLine("Stopping Golem");
            await golem.Stop();

            var golemStopCounter = 0;
            while ((status = await statusChannel.Reader.ReadAsync()) == GolemStatus.Ready && golemStopCounter++ < 10)
            {
                Console.WriteLine("Still stopping");
            }
            Assert.Equal(status, GolemStatus.Off);
        }

        public async Task DisposeAsync()
        {
            if (_requestor != null)
            {
                await _requestor.Stop();
            }
            if (_relay != null)
            {
                await _relay.Stop();
            }
        }

        public void Dispose()
        {
            XunitContext.Flush();
        }
    }
}
