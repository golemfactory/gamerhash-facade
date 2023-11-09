using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MockGUI.ViewModels;
using CommandLine;

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
                       DataContext = new GolemViewModel(o.GolemPath ?? "")
                   };
               });


        }

        base.OnFrameworkInitializationCompleted();
    }
}