using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using GolemLib;

namespace MockGUI.ViewModels
{
    public class GolemViewModel : INotifyPropertyChanged
    {
        public IGolem Golem { get; init; }
        public DateTime DateSince { get; set; } = DateTime.Now;
        public TimeSpan TimeSince { get; set; } = DateTime.Now.TimeOfDay;

        private ObservableCollection<IJob> _jobsHistory;
        public ObservableCollection<IJob> JobsHistory
        {
            get { return _jobsHistory; }
            set { _jobsHistory = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public GolemViewModel(string modulesDir)
        {
            var binaries = Path.Combine(modulesDir, "golem");
            var datadir = Path.Combine(modulesDir, "golem-data");

            Golem = new Golem.Golem(binaries, datadir);
            _jobsHistory = new ObservableCollection<IJob>();
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



