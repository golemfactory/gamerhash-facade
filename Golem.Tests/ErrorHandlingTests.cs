using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Channels;
using System;
using System.Management;

using Golem.Tools;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

namespace Golem.Tests
{
    [Collection(nameof(SerialTestCollection))]
    public class ErrorHandlingTests : JobsTestBase
    {

        public ErrorHandlingTests(ITestOutputHelper outputHelper, GolemFixture golemFixture)
            : base(outputHelper, golemFixture)
        { }

        [Fact]
        public async Task StartTriggerErrorStop_VerifyStatusAsync()
        {
            var logger = _loggerFactory.CreateLogger(nameof(StartTriggerErrorStop_VerifyStatusAsync));

            string golemPath = await PackageBuilder.BuildTestDirectory();
            logger.LogInformation("Path: " + golemPath);

            var golem = await TestUtils.Golem(golemPath, _loggerFactory);
            var statusChannel = TestUtils.PropertyChangeChannel<Golem, GolemStatus?>((Golem)golem, nameof(Golem.Status), _loggerFactory);

            var jobStatusChannel = PropertyChangeChannel<IJob, JobStatus>(null, "");

            Channel<Job?> jobChannel = PropertyChangeChannel(golem, nameof(IGolem.CurrentJob), (Job? currentJob) =>
            {
                jobStatusChannel = PropertyChangeChannel(currentJob, nameof(currentJob.Status),
                    (JobStatus v) => _logger.LogInformation($"Current job Status update: {v}"));

            });

            var startTask = golem.Start();
            Assert.Equal(GolemStatus.Starting, await TestUtils.ReadChannel<GolemStatus?>(statusChannel));
            await startTask;
            Assert.Equal(GolemStatus.Ready, await TestUtils.ReadChannel<GolemStatus?>(statusChannel));

            _logger.LogInformation("Starting Sample App");
            var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // Await for current job in Computing state
            Job? currentJob = await ReadChannel<Job?>(jobChannel);
            var currentState = await ReadChannel(jobStatusChannel, (JobStatus s) => s != JobStatus.Computing, 30_000);

            var providerPidFile = Path.Combine(golemPath, "modules", "golem-data", "provider", "ya-provider.pid");
            var providerPidTxt = await TestUtils.WaitForFileAndRead(providerPidFile);
            var providerPid = Int32.Parse(providerPidTxt);
            var providerProcess = Process.GetProcessById(providerPid);

            var subprocesses = FindSubprocesses(providerPid);
            Assert.NotEmpty(subprocesses);
            Assert.True(IsRunning(subprocesses));


            providerProcess.Kill();

            Assert.Equal(GolemStatus.Error, await TestUtils.ReadChannel<GolemStatus?>(statusChannel));

            Assert.False(IsRunning(subprocesses));

            var stopTask = golem.Stop();
            Assert.Equal(GolemStatus.Stopping, await TestUtils.ReadChannel<GolemStatus?>(statusChannel));
            await stopTask;

            Assert.Equal(GolemStatus.Off, await TestUtils.ReadChannel<GolemStatus?>(statusChannel));
        }

        public ManagementObjectCollection FindSubprocesses(int pid)
        {
            var searcher = new ManagementObjectSearcher(
                "SELECT * " +
                "FROM Win32_Process " +
                "WHERE ParentProcessId=" + pid);
            return searcher.Get();
        }

        public static bool IsRunning(ManagementObjectCollection processes)
        {
            foreach (var proc in processes)
            {
                var pid = (UInt32)proc["ProcessId"];
                if (IsRunning(Convert.ToInt32(pid)))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsRunning(int pid)
        {
            try
            {
                Process.GetProcessById(pid).Dispose();
                return true;
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException)
            {
                return false;
            }
        }
    }

}
