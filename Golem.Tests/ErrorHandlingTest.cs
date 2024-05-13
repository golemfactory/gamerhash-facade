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
using System.Runtime.InteropServices;

namespace Golem.Tests
{
    public class ErrorHandlingTests : JobsTestBase
    {

        public ErrorHandlingTests(ITestOutputHelper outputHelper, GolemFixture golemFixture)
            : base(outputHelper, golemFixture, nameof(ErrorHandlingTests))
        { }

        [OsFact(nameof(OSPlatform.Windows))]
        public async Task StartTriggerErrorStop_VerifyStatusAsync()
        {
            // Having
            string golemPath = await PackageBuilder.BuildTestDirectory();
            _logger.LogInformation("Path: " + golemPath);

            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.Local);

            var statusChannel = StatusChannel(golem);

            var jobChannel = JobChannel(golem);

            // Then
            await StartGolem(golem, golemPath, statusChannel);

            _logger.LogInformation("Starting Sample App");
            var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // Await for current job to be in Computing state
            Job? currentJob = await ReadChannel<Job?>(jobChannel);
            Assert.NotNull(currentJob);
            var jobStatusChannel = JobStatusChannel(currentJob);
            Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel, (JobStatus s) => s == JobStatus.Idle || s == JobStatus.DownloadingModel, 60_000));

            // Access Provider process
            var providerPidFile = Path.Combine(golemPath, "modules", "golem-data", "provider", "ya-provider.pid");
            var providerPidTxt = await TestUtils.WaitForFileAndRead(providerPidFile);
            var providerPid = Int32.Parse(providerPidTxt);
            var providerProcess = Process.GetProcessById(providerPid);

            var subprocesses = FindSubprocesses(providerPid);
            Assert.Contains("ya-runtime-ai.exe", RunningExecutablesNames(subprocesses));

            // Kill Provider process
            providerProcess.Kill();

            // Check if Provider failure triggered status change to Error
            Assert.Equal(GolemStatus.Error, await ReadChannel<GolemStatus>(statusChannel));

            // Check if there are no remaining child processes alive
            Assert.Empty(RunningExecutablesNames(subprocesses));

            // Check if status changed from Error to Off
            await StopGolem(golem, golemPath, statusChannel);

            // Restarting to have Golem again in a Ready state
            await StartGolem(golem, golemPath, statusChannel);

            // After startup Yagna will check all activities and update their states. It does not happen instantly.
            var jobs = new List<IJob>();
            for (int i = 0; i < 10; i++)
            {
                jobs = await golem.ListJobs(DateTime.MinValue);
                // Restarted Golem should list one job from previous run.
                Assert.Single(jobs);
                if (jobs[0].Status != JobStatus.Computing)
                {
                    break;
                }
                await Task.Delay(1_000);
            }

            // Restarted Yagna should update not Teminated Activities from Ready to Unresponsive state, which results with Interrupted job status.
            Assert.True(JobStatus.Interrupted == jobs[0].Status || JobStatus.Finished == jobs[0].Status);

            // Final stop
            await StopGolem(golem, golemPath, statusChannel);
        }

        /// <summary>
        /// Returns subprocesses for given `pid`. Works only on Windows.
        /// </summary>
        public ManagementObjectCollection FindSubprocesses(int pid)
        {
            var searcher = new ManagementObjectSearcher(
                "SELECT * " +
                "FROM Win32_Process " +
                "WHERE ParentProcessId=" + pid);
            return searcher.Get();
        }

        /// <returns>Sorted list of running binaries filenames</returns>
        public static List<String> RunningExecutablesNames(ManagementObjectCollection processes)
        {
            var executables = new List<String>();
            foreach (var proc in processes)
            {
                var pid = (UInt32)proc["ProcessId"];
                if (IsRunning(Convert.ToInt32(pid)))
                {
                    var executable = (String)proc["ExecutablePath"];
                    var executableFile = Path.GetFileName(executable);
                    executables.Add(executableFile);
                }
            }
            executables.Sort();
            return executables;
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
