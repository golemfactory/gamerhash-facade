using System.Reflection;
using System.Runtime.CompilerServices;

using Golem.Tools;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

namespace Golem.Tests
{
    [Collection(nameof(SerialTestCollection))]
    public class GolemTests : IDisposable, IClassFixture<GolemFixture>
    {
        private readonly TestLoggerProvider _loggerProvider;
        private readonly string _golemLib;
        private readonly ITestOutputHelper output;


        public GolemTests(ITestOutputHelper outputHelper, GolemFixture golemFixture)
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

            _golemLib = Path.Combine(dir, "Golem.dll");

            XunitContext.Register(outputHelper);
            output = outputHelper;

            _loggerProvider = new TestLoggerProvider(golemFixture.Sink);
        }

        ILoggerFactory CreateLoggerFactory([CallerMemberName] string testName = "test")
        {
            var logfile = Path.Combine(PackageBuilder.TestDir(testName), testName + "-{Date}.log");
            return LoggerFactory.Create(builder => builder
                            .AddSimpleConsole(options => options.SingleLine = true)
                            .AddFile(logfile)
                            .AddProvider(_loggerProvider));
        }

        [Fact]
        public async Task StartStop_VerifyStatusAsync()
        {
            var testName = nameof(StartStop_VerifyStatusAsync);
            var loggerFactory = CreateLoggerFactory(testName);

            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            output.WriteLine("Path: " + golemPath);

            var golem = await TestUtils.Golem(golemPath, loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), updateStatus, loggerFactory).Subscribe();

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
            var testName = nameof(LoadBinaryStartAndStop_VerifyStatusAsync);
            var loggerFactory = CreateLoggerFactory(testName);

            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            Console.WriteLine("Path: " + golemPath);

            var golem = await TestUtils.LoadBinaryLib(_golemLib, PackageBuilder.ModulesDir(golemPath), loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), updateStatus, loggerFactory).Subscribe();

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
            var testName = nameof(StartAndStopWithoutWaiting_VerifyStatusAsync);
            var loggerFactory = CreateLoggerFactory(testName);

            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            output.WriteLine("Path: " + golemPath);

            var golem = await TestUtils.Golem(golemPath, loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), updateStatus, loggerFactory).Subscribe();

            var startTask = golem.Start();
            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }

        [Fact]
        public async Task TestDownloadArtifacts()
        {
            var dir = await PackageBuilder.BuildTestDirectory();

            Assert.True(Directory.EnumerateFiles(dir, "modules/golem/yagna*").Any());
            Assert.True(Directory.EnumerateFiles(dir, "modules/golem/ya-provider*").Any());
            Assert.True(Directory.EnumerateFiles(dir, "modules/plugins/ya-runtime-ai*").Any());
            Assert.True(Directory.EnumerateFiles(dir, "modules/plugins/dummy*").Any());
        }

        [Fact]
        public async Task Start_ChangeWallet_VerifyStatusAsync()
        {
            var loggerFactory = CreateLoggerFactory();
            string golemPath = await PackageBuilder.BuildTestDirectory();
            Console.WriteLine("Path: " + golemPath);

            var golem = await TestUtils.Golem(golemPath, loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), updateStatus, loggerFactory).Subscribe();

            await golem.Start();

            golem.WalletAddress = "0x1234567890123456789012345678901234567890";

            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }

        [Fact]
        public async Task Start_ChangePrices_VerifyPriceAsync()
        {
            var loggerFactory = CreateLoggerFactory();
            string golemPath = await PackageBuilder.BuildTestDirectory();
            Console.WriteLine("Path: " + golemPath);

            var golem = await TestUtils.Golem(golemPath, loggerFactory);
            await golem.Start();

            ChangePrices_VerifyPrice(golem, loggerFactory);

            await golem.Stop();
        }

        [Fact]
        public async Task DoNotStart_ChangePrices_VerifyPriceAsync()
        {
            var loggerFactory = CreateLoggerFactory();
            string golemPath = await PackageBuilder.BuildTestDirectory();
            Console.WriteLine("Path: " + golemPath);

            var golem = await TestUtils.Golem(golemPath, loggerFactory);
            ChangePrices_VerifyPrice(golem, loggerFactory);
        }

        [Fact]
        public async Task InitPrice_ChangeOnePreset_VerifyPriceAsync()
        {
            var loggerFactory = CreateLoggerFactory();
            string golemPath = await PackageBuilder.BuildTestDirectory();
            Console.WriteLine("Path: " + golemPath);

            var golem = await TestUtils.Golem(golemPath, loggerFactory);

            // Create new runtime descriptor with name "dummy_copy"
            var dummyDescPath = Path.Combine(golemPath, "modules", "plugins", "ya-dummy-ai.json");
            var copyDescPath = Path.Combine(golemPath, "modules", "plugins", "ya-dummy-ai_copy.json");
            File.Copy(dummyDescPath, copyDescPath);
            var copyDescJson = File.ReadAllText(copyDescPath);
            var copyDescObj = JArray.Parse(copyDescJson);
            var copyDescName = copyDescObj.First?.SelectToken("name");
            var presetName = "dummy_copy";
            copyDescName?.Replace(presetName);
            var copyDescStr = copyDescObj.ToString();
            File.WriteAllText(copyDescPath, copyDescStr);

            // Start and stop golem to create and activate new presets
            await golem.Start();
            await golem.Stop();

            // Update price in newly created "dummy_copy" preset
            var presetsPath = Path.Combine(golemPath, "modules", "golem-data", "provider", "presets.json");
            var presetsJson = File.ReadAllText(presetsPath);
            var presetsObj = JObject.Parse(presetsJson);
            var copyPresetGpuPriceObj = queryGpuPrice(presetsObj, presetName);
            var copyPresetGpuPriceOriginalValue = copyPresetGpuPriceObj?.Value<decimal>();
            decimal copyPresetGpuPriceModifiedValue = 21.37m;
            copyPresetGpuPriceObj?.Replace(copyPresetGpuPriceModifiedValue);
            var presets_str = presetsObj?.ToString();
            File.WriteAllText(presetsPath, presets_str);

            // Verify if preset price was saved in preset file
            var copyPresetGpuPriceUpdatedValue = parseGpuPriceFromPreset(presetsPath, presetName);
            Assert.Equal(copyPresetGpuPriceModifiedValue, copyPresetGpuPriceUpdatedValue);

            // Golem on initialization should unify prices in all presets 
            // (it takes price from first Preset, and sets the same price for others if different)
            golem = await TestUtils.Golem(golemPath, loggerFactory);
            var price = golem.Price;

            Assert.Equal(copyPresetGpuPriceOriginalValue, price.GpuPerSec);

            // Verify if "dummy_copy" gpu price got reverted in preset file.
            copyPresetGpuPriceUpdatedValue = parseGpuPriceFromPreset(presetsPath, presetName);
            // Price should be reverted.
            Assert.Equal(copyPresetGpuPriceOriginalValue, copyPresetGpuPriceUpdatedValue);
        }

        JToken? queryGpuPrice(JObject presetsObj, string presetName)
        {
            var presetQuery = String.Format("$.presets[?(@.name == '{0}')]", presetName);
            return presetsObj.SelectToken(presetQuery)?["usage-coeffs"]?["golem.usage.gpu-sec"];
        }

        decimal? parseGpuPriceFromPreset(String presetsPath, String presetName)
        {
            var presetsJson = File.ReadAllText(presetsPath);
            var presetsObj = JObject.Parse(presetsJson);
            var copyPresetGpuPriceObj = queryGpuPrice(presetsObj, presetName);
            return copyPresetGpuPriceObj?.Value<decimal>();
        }

        void ChangePrices_VerifyPrice(IGolem golem, ILoggerFactory loggerFactory)
        {
            decimal price = 0;

            Action<decimal> updatePrice = (v) => price = v;

            golem.Price.PropertyChanged += new PropertyChangedHandler<GolemPrice, decimal>(nameof(GolemPrice.StartPrice), updatePrice, loggerFactory).Subscribe();
            golem.Price.PropertyChanged += new PropertyChangedHandler<GolemPrice, decimal>(nameof(GolemPrice.GpuPerSec), updatePrice, loggerFactory).Subscribe();
            golem.Price.PropertyChanged += new PropertyChangedHandler<GolemPrice, decimal>(nameof(GolemPrice.EnvPerSec), updatePrice, loggerFactory).Subscribe();
            golem.Price.PropertyChanged += new PropertyChangedHandler<GolemPrice, decimal>(nameof(GolemPrice.NumRequests), updatePrice, loggerFactory).Subscribe();

            //Assert property changes
            golem.Price.StartPrice = 0.005m;
            Assert.Equal(0.005m, price);

            golem.Price.GpuPerSec = 0.006m;
            Assert.Equal(0.006m, price);

            golem.Price.EnvPerSec = 0.007m;
            Assert.Equal(0.007m, price);

            golem.Price.NumRequests = 0.008m;
            Assert.Equal(0.008m, price);

            //Assert property returns correct value
            Assert.Equal(0.005m, golem.Price.StartPrice);
            Assert.Equal(0.006m, golem.Price.GpuPerSec);
            Assert.Equal(0.007m, golem.Price.EnvPerSec);
            Assert.Equal(0.008m, golem.Price.NumRequests);
        }

        public void Dispose()
        {
            XunitContext.Flush();
        }
    }
}
