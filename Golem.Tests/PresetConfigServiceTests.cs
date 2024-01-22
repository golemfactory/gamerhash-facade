

using Golem.Tools;
using Golem.Yagna;

using Microsoft.Extensions.Logging;

using Moq;

namespace Golem.Tests
{
    [Collection(nameof(SerialTestCollection))]
    public class PresetsTests : IDisposable, IClassFixture<GolemFixture>
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        public PresetsTests(ITestOutputHelper outputHelper, GolemFixture golemFixture)
        {
            XunitContext.Register(outputHelper);
            // Log file directly in `tests` directory (like `tests/Jobtests-20231231.log )
            var logfile = Path.Combine(PackageBuilder.TestDir(""), nameof(JobTests) + "-{Date}.log");
            var loggerProvider = new TestLoggerProvider(golemFixture.Sink);
            _loggerFactory = LoggerFactory.Create(builder => builder
                //// Console logger makes `dotnet test` hang on Windows
                // .AddSimpleConsole(options => options.SingleLine = true)
                .AddFile(logfile)
                .AddProvider(loggerProvider)
            );
            _logger = _loggerFactory.CreateLogger(nameof(JobTests));
        }

        [Fact]
        public void InitilizeDefaultPresets_ActivePresetIsOtherThanDefault_DoNotCreateNewOnesAndActivateDeactivateCorrectOnes()
        {
            var provider = new Mock<IProvider>();
            provider
                .Setup(p => p.Exec<List<string>>(It.Is<IEnumerable<object>>(s => 
                    s.SequenceEqual("--json preset active".Split()))))
                .Returns(new List<string>
                {
                    "ai"
                });

            provider
                .Setup(p => p.Exec<List<Preset>>(It.Is<IEnumerable<object>>(s => 
                    s.SequenceEqual("preset --json list".Split()))))
                .Returns(new List<Preset>
                {
                    new Preset("dummy", "dummy", new Dictionary<string, decimal>()),
                    new Preset("ai", "dummy", new Dictionary<string, decimal>())
                });

            provider
                .Setup(p => p.Exec<List<ExeUnit>>(It.Is<IEnumerable<object>>(s => 
                    s.SequenceEqual("--json exe-unit list".Split()))))
                .Returns(new List<ExeUnit>
                {
                    new ExeUnit("dummy", "ver1")
                });

            var config = new PresetConfigService(provider.Object);

            config.InitilizeDefaultPresets();

            provider
                .Verify(p => p.ExecToText(
                            It.Is<IEnumerable<String>>(s => 
                                s.Contains("preset") && s.Contains("create")
                            )
                        ),
                        Times.Never
                    );

            provider
                .Verify(p => p.ExecToText(
                            It.Is<IEnumerable<String>>(s => 
                                s.Contains("preset") && s.Contains("activate") && s.Contains("dummy")
                            )
                        ),
                        Times.Once
                    );

            provider
                .Verify(p => p.ExecToText(
                            It.Is<IEnumerable<String>>(s => 
                                s.Contains("preset") && s.Contains("deactivate") && s.Contains("ai")
                            )
                        ),
                        Times.Once
                    );
        }

        public void Dispose()
        {
            XunitContext.Flush();
        }
    }
}