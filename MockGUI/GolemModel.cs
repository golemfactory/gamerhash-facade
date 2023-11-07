using System.ComponentModel;
using System.Runtime.CompilerServices;
using GolemLib;

namespace MockGUI.ViewModels
{
    public class GolemViewModel : INotifyPropertyChanged
    {
        public IGolem golem { get; } = new Golem.Golem("/home/nieznanysprawiciel/.local/bin/", null);

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}



