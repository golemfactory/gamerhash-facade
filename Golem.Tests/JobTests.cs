using App;

using Golem;
using Golem.IntegrationTests.Tools;
using Golem.Yagna;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

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
            _loggerFactory = LoggerFactory.Create(builder =>
                builder.AddSimpleConsole(options => options.SingleLine = true)
            );
        }

        public async Task InitializeAsync()
        {
            _relay = await GolemRelay.Build(nameof(JobTests));
            Assert.True(_relay.Start());
            System.Environment.SetEnvironmentVariable("YA_NET_RELAY_HOST","127.0.0.1:17464");
            Thread.Sleep(1000);
            
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
            
            GolemStatus status = GolemStatus.Off;
            Action<GolemStatus> golemStatus = (v) =>
            {
                Console.WriteLine("Golem status update. {0}", v);
                status = v;
            };
            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), golemStatus).Subscribe();

            IJob? currentJob = null;
            Action<IJob?> currentJobHook = (v) =>
            {
                Console.WriteLine("Current Job update. {0}", v);
                currentJob = v;
            };
            golem.PropertyChanged += new PropertyChangedHandler<Golem, IJob?>(nameof(IGolem.CurrentJob), currentJobHook).Subscribe();

            Console.WriteLine("Starting Golem");
            await golem.Start();
            Assert.Equal(GolemStatus.Ready, status);
            Assert.Null(golem.CurrentJob);

            Console.WriteLine("Starting App");
            var app_process = _requestor?.CreateAppProcess() ?? throw new Exception("Requestor not started yet");
            Assert.True(app_process.Start());
            
            Thread.Sleep(10000);

            Assert.NotNull(golem.CurrentJob);

            Console.WriteLine("Stopping App");
            app_process.Kill();
            Thread.Sleep(3000);

            Assert.Null(golem.CurrentJob);

            Console.WriteLine("Stopping Golem");
            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
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
