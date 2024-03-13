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
    [Option('r', "relay", Default = RelayType.Public, Required = false, HelpText = "Change relay to devnet yacn2a or setup local")]
    public required RelayType Relay { get; set; }
    [Option('m', "mainnet", Default = false, Required = false, HelpText = "Enables usage of mainnet")]
    public bool Mainnet { get; set; }
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
            _ = Parser.Default.ParseArguments<AppArguments>(desktop.Args)
            .WithParsed<AppArguments>(args =>
               {
                   desktop.MainWindow = new MainWindow
                   {
                       DataContext = null
                   };

                   new Task(async () =>
                   {
                       GolemViewModel view = args.UseDll ?
                           await GolemViewModel.Load(args.GolemPath ?? "", args.Relay, args.Mainnet) :
                           await GolemViewModel.CreateStatic(args.GolemPath ?? "", args.Relay, args.Mainnet);

                       desktop.MainWindow.Closing += new ShutdownHook(view).OnShutdown;

                       Dispatcher.UIThread.Post(() =>
                           desktop.MainWindow.DataContext = view
                       );
                   }).Start();

               });


        }

        base.OnFrameworkInitializationCompleted();
    }
}

class ShutdownHook
{
    private readonly GolemViewModel view;

    public ShutdownHook(GolemViewModel view)
    {
        this.view = view;
    }

    public void OnShutdown(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        new Task(async () =>
        {
            await view.Shutdown();
        }).Start();
    }
}
