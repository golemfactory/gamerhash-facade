using Golem;
using Golem.IntegrationTests.Tools;
using GolemLib;
using GolemLib.Types;

namespace Golem.Tests
{
    public class GolemTests
    {
        string golemPath = "d:\\code\\yagna\\target\\debug";

        [Fact]
        public async Task StartStop_VerifyStatusAsync()
        {
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(golemPath);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) => { 
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<GolemStatus>(nameof(IGolem.Status), updateStatus).Subscribe();

            await golem.Start();

            Assert.Equal(GolemStatus.Ready, status);

            Console.WriteLine("Sleep for a second.");
            Thread.Sleep(1_000);

            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }

        [Fact]
        public async Task Job_verifyStatusAsync()
        {
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(golemPath);
            
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) => { 
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<GolemStatus>(nameof(IGolem.Status), updateStatus).Subscribe();

            await golem.Start();

            Assert.Equal(GolemStatus.Ready, status);
            Assert.Null(golem.CurrentJob);

            Console.WriteLine("Sleep for a second.");
            Thread.Sleep(1_000);

            //TODO: start job

            var current_job = golem.CurrentJob;
            Assert.NotNull(current_job);
            Console.WriteLine("{}", current_job.Status);

            JobStatus job_status = JobStatus.Idle;
            Action<JobStatus> update_Job_Status = (v) => { 
                job_status = v;
            };
            current_job.PropertyChanged += new PropertyChangedHandler<JobStatus>(nameof(IJob.Status), update_Job_Status).Subscribe();

            Assert.Equal(JobStatus.Computing, job_status);

            //TODO: stop job

            Assert.Equal(JobStatus.Finished, job_status);

            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }
    }
}
