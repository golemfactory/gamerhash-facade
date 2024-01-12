using System.Reflection;

using App;

using Golem;
using Golem.Tools;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

namespace Golem.Tests
{
    [Collection("Sequential")]
    public class GolemTests : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly string _golemLib;

        public GolemTests(ITestOutputHelper outputHelper)
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

            _golemLib = Path.Combine(dir, "Golem.dll");

            XunitContext.Register(outputHelper);
            _loggerFactory = LoggerFactory.Create(builder =>
                builder.AddSimpleConsole(options => options.SingleLine = true)
            );
        }

        async Task<IGolem> LoadBinaryLib(string dllPath, string modulesDir, ILoggerFactory? loggerFactory)
        {
            const string factoryType = "Golem.Factory";

            Assembly ass = Assembly.LoadFrom(dllPath);
            Type? t = ass.GetType(factoryType) ?? throw new Exception("Factory Type not found. Lib not loaded: " + dllPath);
            var obj = Activator.CreateInstance(t) ?? throw new Exception("Creating Factory instance failed. Lib not loaded: " + dllPath);
            var factory = obj as IFactory ?? throw new Exception("Cast to IFactory failed.");

            return await factory.Create(modulesDir, loggerFactory);
        }

        [Fact]
        public async Task StartStop_VerifyStatusAsync()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory("StartStop_VerifyStatusAsync");
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), _loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), updateStatus, _loggerFactory).Subscribe();

            var startTask = golem.Start();
            Assert.Equal(GolemStatus.Starting, status);
            await startTask;
            Assert.Equal(GolemStatus.Ready, status);


            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }

        [Fact]
        public async Task LoadBinaryStartAndStop_VerifyStatusAsync()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory("LoadBinaryStartAndStop_VerifyStatusAsync");
            Console.WriteLine("Path: " + golemPath);

            var golem = await LoadBinaryLib(_golemLib, PackageBuilder.ModulesDir(golemPath), _loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), updateStatus, _loggerFactory).Subscribe();

            var startTask = golem.Start();
            Assert.Equal(GolemStatus.Starting, status);
            await startTask;
            
            Assert.Equal(GolemStatus.Ready, status);
            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }

        [Fact]
        public async Task StartAndStopWithoutWaiting_VerifyStatusAsync()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory("StartAndStopWithoutWaiting_VerifyStatusAsync");
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.ModulesDir(golemPath), _loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), updateStatus, _loggerFactory).Subscribe();

            var startTask = golem.Start();
            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }

        [Fact]
        public async Task TestDownloadArtifacts()
        {
            var dir = await PackageBuilder.BuildTestDirectory("TestDownloadArtifacts");

            Assert.True(Directory.EnumerateFiles(dir, "modules/golem/yagna*").Any());
            Assert.True(Directory.EnumerateFiles(dir, "modules/golem/ya-provider*").Any());
            Assert.True(Directory.EnumerateFiles(dir, "modules/plugins/ya-runtime-ai*").Any());
            Assert.True(Directory.EnumerateFiles(dir, "modules/plugins/dummy*").Any());
        }

        [Fact]
        public async Task Start_ChangeWallet_VerifyStatusAsync()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory("Start_ChangeWallet_VerifyStatusAsync");
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), _loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), updateStatus, _loggerFactory).Subscribe();

            await golem.Start();

            golem.WalletAddress = "0x1234567890123456789012345678901234567890";

            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }

        [Fact]
        public async Task Start_ChangePrices_VerifyPriceAsync()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory("Start_ChangePrices_VerifyPriceAsync");
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), _loggerFactory);
            await golem.Start();

            decimal price = 0;

            Action<decimal> updatePrice = (v) => price = v;

            golem.Price.PropertyChanged += new PropertyChangedHandler<GolemPrice, decimal>(nameof(GolemPrice.StartPrice), updatePrice, _loggerFactory).Subscribe();
            golem.Price.PropertyChanged += new PropertyChangedHandler<GolemPrice, decimal>(nameof(GolemPrice.GpuPerHour), updatePrice, _loggerFactory).Subscribe();
            golem.Price.PropertyChanged += new PropertyChangedHandler<GolemPrice, decimal>(nameof(GolemPrice.EnvPerHour), updatePrice, _loggerFactory).Subscribe();
            golem.Price.PropertyChanged += new PropertyChangedHandler<GolemPrice, decimal>(nameof(GolemPrice.NumRequests), updatePrice, _loggerFactory).Subscribe();


            //Assert property changes
            golem.Price.StartPrice = 0.005m;
            Assert.Equal(0.005m, price);

            golem.Price.GpuPerHour = 0.006m;
            Assert.Equal(0.006m, price);

            golem.Price.EnvPerHour = 0.007m;
            Assert.Equal(0.007m, price);

            golem.Price.NumRequests = 0.008m;
            Assert.Equal(0.008m, price);

            //Assert property returns correct value
            Assert.Equal(0.005m, golem.Price.StartPrice);
            Assert.Equal(0.006m, golem.Price.GpuPerHour);
            Assert.Equal(0.007m, golem.Price.EnvPerHour);
            Assert.Equal(0.008m, golem.Price.NumRequests);

            await golem.Stop();
        }

        public void Dispose()
        {
            XunitContext.Flush();
        }
    }
}
