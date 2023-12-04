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
    [Option('g', "golem", Required = true, HelpText = "Path to a folder with golem executables")]
    public string? GolemPath { get; set; }
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
                       var view = await GolemViewModel.Create(o.GolemPath ?? "");
                       Dispatcher.UIThread.Post(() =>
                            desktop.MainWindow.DataContext = view
                           );
                   }).Start();

               });


        }

        base.OnFrameworkInitializationCompleted();
    }
}