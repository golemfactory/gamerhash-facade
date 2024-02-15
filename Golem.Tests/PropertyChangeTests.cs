using System.Reflection;

using Golem.Tools;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

namespace Golem.Tests
{
    [Collection(nameof(SerialTestCollection))]
    public class PropertyChangeTests : IDisposable, IClassFixture<GolemFixture>
    {
        private readonly TestLoggerProvider _loggerProvider;
        private readonly string _golemLib;
        private readonly ITestOutputHelper output;


        public PropertyChangeTests(ITestOutputHelper outputHelper, GolemFixture golemFixture)
        {

            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

            _golemLib = Path.Combine(dir, "Golem.dll");

            XunitContext.Register(outputHelper);
            output = outputHelper;

            _loggerProvider = new TestLoggerProvider(golemFixture.Sink);
        }

        ILoggerFactory CreateLoggerFactory(string testName)
        {
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
        public async Task VerifyNetworkOnPropertyChange()
        {
            var testName = nameof(VerifyNetworkOnPropertyChange);
            var loggerFactory = CreateLoggerFactory(testName);
            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            var golem = await LoadBinaryLib(_golemLib, PackageBuilder.ModulesDir(golemPath), loggerFactory);

            // Initialize property value
            golem.NetworkSpeed = 1u;

            var property = uint.MaxValue;
            Action<uint> update = (v) =>
            {
                property = v;
            };
            golem.PropertyChanged += new PropertyChangedHandler<Golem, uint>(nameof(IGolem.NetworkSpeed), update, loggerFactory).Subscribe();

            // Setting same value should not trigger property change event
            golem.NetworkSpeed = 1u;
            Assert.Equal(uint.MaxValue, property);

            // Setting different value should trigger property change event
            golem.NetworkSpeed = 2u;
            Assert.Equal(2u, property);
        }

        /*
        [Fact]
        public async Task VerifyWalletAddressOnPropertyChange()
        {
            var testName = nameof(VerifyWalletAddressOnPropertyChange);
            var loggerFactory = CreateLoggerFactory(testName);
            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            var golem = await LoadBinaryLib(_golemLib, PackageBuilder.ModulesDir(golemPath), loggerFactory);

            // TODO Without starting fails on Provider.Config set.
            await golem.Start();

            // Initialize property value
            golem.WalletAddress = "0x000000000000000000000000000000000000001";

            var property = "0x000000000000000000000000000000000000002";
            Action<string?> update = (v) =>
            {
                property = v;
            };
            golem.PropertyChanged += new PropertyChangedHandler<Golem, string>(nameof(IGolem.WalletAddress), update, loggerFactory).Subscribe();

            // Setting same value should not trigger property change event
            golem.WalletAddress = "0x000000000000000000000000000000000000001";
            Assert.Equal("0x000000000000000000000000000000000000002", property);

            // Setting different value should trigger property change event
            golem.WalletAddress = "0x000000000000000000000000000000000000003";
            Assert.Equal("0x000000000000000000000000000000000000003", property);
            
            await golem.Stop();
        }
        */
        
        [Fact]
        public async Task VerifyGolemPriceOnPropertyChange()
        {
            var testName = nameof(VerifyGolemPriceOnPropertyChange);
            var loggerFactory = CreateLoggerFactory(testName);
            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            var golem = await LoadBinaryLib(_golemLib, PackageBuilder.ModulesDir(golemPath), loggerFactory);

            // Initialize property value
            var initialValue = new GolemPrice
            {
                StartPrice = 1,
                GpuPerHour = 1,
                EnvPerHour = 1,
                NumRequests = 1
            };
            golem.Price = initialValue;

            var property = new GolemPrice
            {
                StartPrice = 2,
                GpuPerHour = 2,
                EnvPerHour = 2,
                NumRequests = 2
            };
            Action<GolemPrice?> update = (v) =>
            {
                property = v;
            };
            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemPrice>(nameof(IGolem.Price), update, loggerFactory).Subscribe();

            // Setting same value should not trigger property change event
            golem.Price = initialValue;
            Assert.Equivalent(new GolemPrice
            {
                StartPrice = 2,
                GpuPerHour = 2,
                EnvPerHour = 2,
                NumRequests = 2
            }, property);

            // Setting different value should trigger property change event
            var newValue = new GolemPrice
            {
                StartPrice = 3,
                GpuPerHour = 3,
                EnvPerHour = 3,
                NumRequests = 3
            };
            golem.Price = newValue;
            Assert.Equivalent(newValue, property);
        }

        public void Dispose()
        {
            XunitContext.Flush();
        }
    }
}
