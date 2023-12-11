using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MockGUI.ViewModels;
using CommandLine;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Avalonia.Threading;

using Golem.Tools;

namespace MockGUI;



public class AppArguments
{
    [Option('g', "golem", Required = true, HelpText = "Path to a folder with golem executables (modules)")]
    public string? GolemPath { get; set; }
    [Option('d', "use-dll", Required = false, HelpText = "Load Golem object from dll found in binaries directory. (Simulates how GamerHash will use it. Otherwise project dependency will be used.)")]
    public bool UseDll { get; set; }
    [Option('r', "relay", Default = RelayType.Devnet, Required = false, HelpText = "Change relay to devnet yacn2a")]
    public required RelayType Relay { get; set; }
}

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Parser.Default.ParseArguments<AppArguments>(desktop.Args)
               .WithParsed<AppArguments>(o =>
               {
                   desktop.MainWindow = new MainWindow
                   {
                       DataContext = null
                   };

                   new Task(async () =>
                   {
                       GolemViewModel view = o.UseDll ? await GolemViewModel.Load(o.GolemPath ?? "", o.Relay) : await GolemViewModel.CreateStatic(o.GolemPath ?? "", o.Relay);
                       Dispatcher.UIThread.Post(() =>
                                desktop.MainWindow.DataContext = view
                               );
                   }).Start();
               });


        }

        base.OnFrameworkInitializationCompleted();
    }
}