using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using GolemLib;

namespace MockGUI.ViewModels
{
    public class GolemViewModel : INotifyPropertyChanged
    {
        public IGolem Golem { get; init; }
        public ObservableCollection<IJob> JobsHistory { get; set; }
        public DateTime DateSince { get; set; } = DateTime.Now;
        public TimeSpan TimeSince { get; set; } = DateTime.Now.TimeOfDay;

        public event PropertyChangedEventHandler? PropertyChanged;

        public GolemViewModel(string modulesDir)
        {
            var binaries = Path.Combine(modulesDir, "golem");
            var datadir = Path.Combine(modulesDir, "golem-data");

            Golem = new Golem.Golem(binaries, datadir);
            JobsHistory = new ObservableCollection<IJob>();
        }

        public void OnStartCommand()
        {
            this.Golem.Start();
        }

        public void OnStopCommand()
        {
            this.Golem.Stop();
        }

        public void OnSuspendCommand()
        {
            this.Golem.Suspend();
        }

        public void OnResumeCommand()
        {
            this.Golem.Resume();
        }

        public void OnRunExample()
        {
            throw new NotImplementedException();
        }

        public async void OnListJobs()
        {
            var since = this.DateSince.Date + this.TimeSince;
            var jobs = await this.Golem.ListJobs(since);
            this.JobsHistory = new ObservableCollection<IJob>(jobs);
        }
    }
}



