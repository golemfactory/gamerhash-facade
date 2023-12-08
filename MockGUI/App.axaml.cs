using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MockGUI.ViewModels;
using CommandLine;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Avalonia.Threading;

namespace MockGUI;

public class AppArguments
{
    [Option('g', "golem", Required = true, HelpText = "Path to a folder with golem executables (modules)")]
    public string? GolemPath { get; set; }
    [Option('d', "use-dll", Required = false, HelpText = "Load Golem object from dll found in binaries directory. (Simulates how GamerHash will use it. Otherwise project dependency will be used.)")]
    public bool UseDll { get; set; }
    [Option('r', "devnet-relay", Default = false, Required = false, HelpText = "Change relay to devnet yacn2a")]
    public required bool DevnetRelay { get; set; }
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

                   if (o.DevnetRelay)
                       System.Environment.SetEnvironmentVariable("YA_NET_RELAY_HOST", "yacn2a.dev.golem.network:7477");


                   if (o.UseDll)
                   {
                       new Task(async () =>
                       {
                           var view = await GolemViewModel.Load(o.GolemPath ?? "");
                           Dispatcher.UIThread.Post(() =>
                                desktop.MainWindow.DataContext = view
                               );
                       }).Start();
                   }
                   else
                   {
                       desktop.MainWindow.DataContext = GolemViewModel.CreateStatic(o.GolemPath ?? "");
                   }
               });


        }

        base.OnFrameworkInitializationCompleted();
    }
}