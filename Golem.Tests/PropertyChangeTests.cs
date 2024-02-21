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

        [Fact]
        public async Task VerifyNetworkOnPropertyChange()
        {
            var testName = nameof(VerifyNetworkOnPropertyChange);
            var loggerFactory = CreateLoggerFactory(testName);
            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            var golem = await TestUtils.LoadBinaryLib(_golemLib, PackageBuilder.ModulesDir(golemPath), loggerFactory);

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

        [Fact]
        public async Task VerifyWalletAddressOnPropertyChange()
        {
            var testName = nameof(VerifyWalletAddressOnPropertyChange);
            var loggerFactory = CreateLoggerFactory(testName);
            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            var golem = await TestUtils.LoadBinaryLib(_golemLib, PackageBuilder.ModulesDir(golemPath), loggerFactory);

            // TODO Without starting fails on Provider.Config set.
            await golem.Start();

            // Initialize property value
            golem.WalletAddress = "0x1111111111111111111111111111111111111111";
            var property = "0x2222222222222222222222222222222222222222";
            Action<string?> update = (v) =>
            {
                property = v;
            };
            golem.PropertyChanged += new PropertyChangedHandler<Golem, string>(nameof(IGolem.WalletAddress), update, loggerFactory).Subscribe();

            // Setting same value should not trigger property change event
            golem.WalletAddress = "0x1111111111111111111111111111111111111111";
            Assert.Equal("0x2222222222222222222222222222222222222222", property);

            // Setting different value should trigger property change event
            golem.WalletAddress = "0x3333333333333333333333333333333333333333";
            Assert.Equal("0x3333333333333333333333333333333333333333", property);

            await golem.Stop();
        }

        [Fact]
        public async Task VerifyGolemPriceOnPropertyChange()
        {
            var testName = nameof(VerifyGolemPriceOnPropertyChange);
            var loggerFactory = CreateLoggerFactory(testName);
            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            var golem = await TestUtils.LoadBinaryLib(_golemLib, PackageBuilder.ModulesDir(golemPath), loggerFactory);

            // Initialize property value
            var initialValue = new GolemPrice
            {
                StartPrice = 1,
                GpuPerSec = 1,
                EnvPerSec = 1,
                NumRequests = 1
            };
            golem.Price = initialValue;

            var property = new GolemPrice
            {
                StartPrice = 2,
                GpuPerSec = 2,
                EnvPerSec = 2,
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
                GpuPerSec = 2,
                EnvPerSec = 2,
                NumRequests = 2
            }, property);

            // Setting different value should trigger property change event
            var newValue = new GolemPrice
            {
                StartPrice = 3,
                GpuPerSec = 3,
                EnvPerSec = 3,
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
