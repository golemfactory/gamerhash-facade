using System.ComponentModel;
using System.IO;
using GolemLib;

namespace MockGUI.ViewModels
{
    public class GolemViewModel : INotifyPropertyChanged
    {
        public IGolem Golem { get; init; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public GolemViewModel(string modulesDir)
        {
            var binaries = Path.Combine(modulesDir, "golem");
            var datadir = Path.Combine(modulesDir, "golem-data");

            Golem = new Golem.Golem(binaries, datadir);
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
    }
}



