using System.Reflection;

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
        // private UnhandledExceptionEventArgs? _unhandledException = null;


        public GolemTests(ITestOutputHelper outputHelper, GolemFixture golemFixture)
        {

            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

            _golemLib = Path.Combine(dir, "Golem.dll");

            XunitContext.Register(outputHelper);
            output = outputHelper;
            
            _loggerProvider = new TestLoggerProvider(golemFixture.Sink);
            
            // AppDomain.CurrentDomain.UnhandledException += SetUnhandledException;
        }

        // public void SetUnhandledException(object sender, UnhandledExceptionEventArgs exception)
        // {
        //     _unhandledException = exception;
        // }

        ILoggerFactory CreateLoggerFactory(string testName) {
            var logfile = Path.Combine(PackageBuilder.TestDir(testName), testName + "-{Date}.log");
            return LoggerFactory.Create(builder => builder
                            .AddSimpleConsole(options => options.SingleLine = true)
                            .AddFile(logfile)
                            .AddProvider(_loggerProvider));
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
            var testName = nameof(StartStop_VerifyStatusAsync);
            var loggerFactory = CreateLoggerFactory(testName);

            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            output.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), loggerFactory);
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

            var golem = await LoadBinaryLib(_golemLib, PackageBuilder.ModulesDir(golemPath), loggerFactory);
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

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.ModulesDir(golemPath), loggerFactory);
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
            var dir = await PackageBuilder.BuildTestDirectory("TestDownloadArtifacts");

            Assert.True(Directory.EnumerateFiles(dir, "modules/golem/yagna*").Any());
            Assert.True(Directory.EnumerateFiles(dir, "modules/golem/ya-provider*").Any());
            Assert.True(Directory.EnumerateFiles(dir, "modules/plugins/ya-runtime-ai*").Any());
            Assert.True(Directory.EnumerateFiles(dir, "modules/plugins/dummy*").Any());
        }

        [Fact]
        public async Task Start_ChangeWallet_VerifyStatusAsync()
        {
            var testName = nameof(Start_ChangeWallet_VerifyStatusAsync);
            var loggerFactory = CreateLoggerFactory(testName);

            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), loggerFactory);
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
            var testName = nameof(Start_ChangePrices_VerifyPriceAsync);
            var loggerFactory = CreateLoggerFactory(testName);

            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), loggerFactory);
            await golem.Start();

            ChangePrices_VerifyPrice(golem, loggerFactory);

            await golem.Stop();
        }

        [Fact]
        public async Task DoNotStart_ChangePrices_VerifyPriceAsync()
        {
            var testName = nameof(DoNotStart_ChangePrices_VerifyPriceAsync);
            var loggerFactory = CreateLoggerFactory(testName);

            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), loggerFactory);
            ChangePrices_VerifyPrice(golem, loggerFactory);
        }

        [Fact]
        public async Task InitPrice_ChangeOnePreset_VerifyPriceAsync()
        {
            var testName = nameof(InitPrice_ChangeOnePreset_VerifyPriceAsync);
            var loggerFactory = CreateLoggerFactory(testName);

            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), loggerFactory);

            // Create new runtime descriptor with name "copy"
            var dummy_desc_path = Path.Combine(golemPath, "modules", "plugins", "ya-dummy-ai.json");
            var copy_desc_path = Path.Combine(golemPath, "modules", "plugins", "ya-dummy-ai_copy.json");
            File.Copy(dummy_desc_path, copy_desc_path);
            var copy_desc_json  = File.ReadAllText(copy_desc_path);
            var copy_desc_obj = Newtonsoft.Json.JsonConvert.DeserializeObject(copy_desc_json) as JArray;
            var copy_desc_name = copy_desc_obj.First.SelectToken("name");
            copy_desc_name.Replace("dummy_copy");
            var copy_desc_str = copy_desc_obj.ToString();
            File.WriteAllText(copy_desc_path, copy_desc_str);

            // Start and stop golem to create and activate new presets
            await golem.Start();
            await golem.Stop();

            // Update price in newly created "dummy_copy" preset
            var presets_path = Path.Combine(golemPath, "modules", "golem-data", "provider", "presets.json");
            var presets_json = File.ReadAllText(presets_path);
            var presets_obj = Newtonsoft.Json.JsonConvert.DeserializeObject(presets_json) as JObject;
            var copy_preset_gpu_price_obj = presets_obj.SelectToken("$.presets[?(@.name == 'dummy_copy')]")["usage-coeffs"]["golem.usage.gpu-sec"];
            var copy_preset_gpu_price_oiginal_value = copy_preset_gpu_price_obj.Value<decimal>();
            decimal copy_preset_gpu_price_modified_value = 21.37m;
            copy_preset_gpu_price_obj.Replace(copy_preset_gpu_price_modified_value);
            var presets_str = presets_obj.ToString();
            File.WriteAllText(presets_path, presets_str);

            // Verify if preset price was saved in preset file
            presets_json = File.ReadAllText(presets_path);
            presets_obj = Newtonsoft.Json.JsonConvert.DeserializeObject(presets_json) as JObject;
            copy_preset_gpu_price_obj = presets_obj.SelectToken("$.presets[?(@.name == 'dummy_copy')]")["usage-coeffs"]["golem.usage.gpu-sec"];
            var copy_preset_gpu_price_updated_value = copy_preset_gpu_price_obj.Value<decimal>();
            Assert.Equal(copy_preset_gpu_price_updated_value, copy_preset_gpu_price_modified_value);

            // Golem on initialization should unify prices in all presets
            golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), loggerFactory);
            var price = golem.Price;

            Assert.Equal(copy_preset_gpu_price_oiginal_value, price.GpuPerHour);

            // Verify if "dummy_copy" gpu price got reverted in preset file.
            presets_json = File.ReadAllText(presets_path);
            presets_obj = Newtonsoft.Json.JsonConvert.DeserializeObject(presets_json) as JObject;
            copy_preset_gpu_price_obj = presets_obj.SelectToken("$.presets[?(@.name == 'dummy_copy')]")["usage-coeffs"]["golem.usage.gpu-sec"];
            copy_preset_gpu_price_updated_value = copy_preset_gpu_price_obj.Value<decimal>();
            // Price should be reverted.
            Assert.Equal(copy_preset_gpu_price_updated_value, copy_preset_gpu_price_oiginal_value);
        }

        void ChangePrices_VerifyPrice(Golem golem, ILoggerFactory loggerFactory)
        {
            decimal price = 0;

            Action<decimal> updatePrice = (v) => price = v;

            golem.Price.PropertyChanged += new PropertyChangedHandler<GolemPrice, decimal>(nameof(GolemPrice.StartPrice), updatePrice, loggerFactory).Subscribe();
            golem.Price.PropertyChanged += new PropertyChangedHandler<GolemPrice, decimal>(nameof(GolemPrice.GpuPerHour), updatePrice, loggerFactory).Subscribe();
            golem.Price.PropertyChanged += new PropertyChangedHandler<GolemPrice, decimal>(nameof(GolemPrice.EnvPerHour), updatePrice, loggerFactory).Subscribe();
            golem.Price.PropertyChanged += new PropertyChangedHandler<GolemPrice, decimal>(nameof(GolemPrice.NumRequests), updatePrice, loggerFactory).Subscribe();

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
        }

        public void Dispose()
        {
            XunitContext.Flush();
        }
    }
}
