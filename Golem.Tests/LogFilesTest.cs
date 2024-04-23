using System.Text.RegularExpressions;
using System.Threading.Channels;

using Golem.Tools;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

namespace Golem.Tests
{
    public class LogFilesTest : JobsTestBase
    {

        public LogFilesTest(ITestOutputHelper outputHelper, GolemFixture golemFixture)
            : base(outputHelper, golemFixture, nameof(LogFilesTest))
        { }

        [Fact]
        public async Task StartRunStopStartStopStartStop_VerifyLogFiles()
        {
            // Having
            var logger = _loggerFactory.CreateLogger(nameof(StartRunStopStartStopStartStop_VerifyLogFiles));

            string golemPath = await PackageBuilder.BuildTestDirectory();

            var golem = await TestUtils.Golem(golemPath, _loggerFactory);

            var status = new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), _loggerFactory).Observe(golem);
            var jobStatusChannel = PropertyChangeChannel<IJob, JobStatus>(null, "");
            Channel<Job?> jobChannel = PropertyChangeChannel(golem, nameof(IGolem.CurrentJob), (Job? currentJob) =>
            {
                jobStatusChannel = PropertyChangeChannel(currentJob, nameof(currentJob.Status),
                    (JobStatus v) => _logger.LogInformation($"Current job Status update: {v}"));
            });

            // Then

            // Before any run there should be no log files.
            var logFiles = golem.LogFiles();
            _logger.LogInformation($"Log files after 2nd run: {String.Join("\n", logFiles)}");
            Assert.Empty(logFiles);

            await StartGolem(golem, golemPath, status);
            await StopGolem(golem, golemPath, status);

            logFiles = golem.LogFiles();
            _logger.LogInformation($"Log files after 1st run: {String.Join("\n", logFiles)}");
            var logFileNames = logFiles.Select(file => Path.GetFileName(file)).ToList() ?? new List<string>();
            // After 1st run there should be yagna and ya-provider log files
            Assert.Contains("ya-provider_rCURRENT.log", logFileNames);
            Assert.Contains("yagna_rCURRENT.log", logFileNames);

            await StartGolem(golem, golemPath, status);

            // Start app to create runtime logs
            var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());
            Job? currentJob = await ReadChannel<Job?>(jobChannel, timeoutMs: 30_000);
            var currentState = await ReadChannel(jobStatusChannel, (JobStatus s) => s != JobStatus.Computing, 30_000);
            await app.Stop();

            await StopGolem(golem, golemPath, status);

            logFiles = golem.LogFiles();
            _logger.LogInformation($"Log files after 2nd run: {String.Join("\n", logFiles)}");
            // After 2nd run there should be runtime logs created by `test`/`offer-template` commands
            var runtimeTestLogFiles = logFiles.FindAll(path => path
                .Contains(Path.Combine("exe-unit", "work", "logs")))
                .FindAll(path => path.EndsWith(".log"));
            Assert.NotEmpty(runtimeTestLogFiles);
            // After 2nd run there should be new runtime log created by running the app
            var runtimeActivityLogFiles = logFiles.FindAll(path => path
                .Contains(Path.Combine("exe-unit", "work")))
                .FindAll(path => path.EndsWith(".log"))
                .FindAll(path => !runtimeTestLogFiles.Contains(path));
            Assert.Single(runtimeActivityLogFiles);
            logFileNames = logFiles.Select(file => Path.GetFileName(file)).ToList() ?? new List<string>();
            // After 2nd run there should be current yagna and ya-provider log files
            Assert.Contains("ya-provider_rCURRENT.log", logFileNames);
            Assert.Contains("yagna_rCURRENT.log", logFileNames);
            // After 2nd run there should be previous yagna and ya-provider log files
            Regex providerLogPattern = new Regex(@"^ya-provider_r[0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{2}-[0-9]{2}-[0-9]{2}\.log$");
            Assert.Single(logFileNames.FindAll(file => providerLogPattern.IsMatch(file)));
            Regex yagnaLogPattern = new Regex(@"^yagna_r[0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{2}-[0-9]{2}-[0-9]{2}\.log$");
            Assert.Single(logFileNames.FindAll(file => yagnaLogPattern.IsMatch(file)));

            await StartGolem(golem, golemPath, status);
            await StopGolem(golem, golemPath, status);

            logFiles = golem.LogFiles();
            _logger.LogInformation($"Log files after 3rd run: {String.Join("\n", logFiles)}");
            // After 3rd run there should be runtime logs created by `test`/`offer-template` commands
            runtimeTestLogFiles = logFiles.FindAll(path => path
                .Contains(Path.Combine("exe-unit", "work", "logs")))
                .FindAll(path => path.EndsWith(".log"));
            Assert.NotEmpty(runtimeTestLogFiles);
            // After 3rd run there should old runtime logs created by previous activity
            runtimeActivityLogFiles = logFiles.FindAll(path => path
                .Contains(Path.Combine("exe-unit", "work")))
                .FindAll(path => path.EndsWith(".log"))
                .FindAll(path => !runtimeTestLogFiles.Contains(path));
            Assert.Single(runtimeActivityLogFiles);
            logFileNames = logFiles.Select(file => Path.GetFileName(file)).ToList() ?? new List<string>();
            // After 3rd run there should be current yagna and ya-provider log files
            Assert.Contains("ya-provider_rCURRENT.log", logFileNames);
            Assert.Contains("yagna_rCURRENT.log", logFileNames);
            // After 3rd run there should be previous yagna and ya-provider log files
            Assert.Single(logFileNames.FindAll(file => providerLogPattern.IsMatch(file)));
            Assert.Single(logFileNames.FindAll(file => yagnaLogPattern.IsMatch(file)));
            // After 3rd run there should be previous previous yagna and ya-provider log gz file archives
            Regex providerLogGzPattern = new Regex(@"^ya-provider_r[0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{2}-[0-9]{2}-[0-9]{2}\.log\.gz$");
            Assert.Single(logFileNames.FindAll(file => providerLogGzPattern.IsMatch(file)));
            Regex yagnaLogGzPattern = new Regex(@"^yagna_r[0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{2}-[0-9]{2}-[0-9]{2}\.log\.gz$");
            Assert.Single(logFileNames.FindAll(file => yagnaLogGzPattern.IsMatch(file)));
        }

        static async Task StartGolem(IGolem golem, String golemPath, PropertyChangedHandler<Golem, GolemStatus> status)
        {
            var startTask = golem.Start();
            Assert.Equal(GolemStatus.Starting, status.Value);
            await startTask;
            Assert.Equal(GolemStatus.Ready, status.Value);
            var providerPidFile = Path.Combine(golemPath, "modules/golem-data/provider/ya-provider.pid");
            await TestUtils.WaitForFile(providerPidFile);
            await Task.Delay(1);
        }

        static async Task StopGolem(IGolem golem, String golemPath, PropertyChangedHandler<Golem, GolemStatus> status)
        {
            var stopTask = golem.Stop();
            await stopTask;
            Assert.Equal(GolemStatus.Off, status.Value);
            var providerPidFile = Path.Combine(golemPath, "modules/golem-data/provider/ya-provider.pid");
            try
            {
                File.Delete(providerPidFile);
            }
            catch { }
        }
    }

}
