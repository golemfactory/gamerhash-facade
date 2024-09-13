using System.ComponentModel;
using System.Diagnostics;

using CommandLine;

using Golem;
using GolemLib;
using Golem.Tools.ViewModels;

using Microsoft.Extensions.Logging;

namespace FacadeHeadlessApp;

public class FacadeAppArguments
{
    [Option('g', "golem", Required = true, HelpText = "Path to a folder with golem executables (modules)")]
    public string? GolemPath { get; set; }
    [Option('d', "use-dll", Required = false, HelpText = "Load Golem object from dll found in binaries directory. (Simulates how GamerHash will use it. Otherwise project dependency will be used.)")]
    public bool UseDll { get; set; }
    [Option('r', "relay", Default = RelayType.Central, Required = false, HelpText = "Change relay to devnet yacn2a or setup local")]
    public required RelayType Relay { get; set; }
    [Option('m', "mainnet", Default = false, Required = false, HelpText = "Enables usage of mainnet")]
    public bool Mainnet { get; set; }
}


internal class Facade
{
    static async Task Main(string[] argsArray)
    {
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
           builder.AddSimpleConsole()
        );

        var logger = loggerFactory.CreateLogger<Facade>();

        var args = Parser.Default.ParseArguments<FacadeAppArguments>(argsArray).Value;
        string golemPath = args.GolemPath ?? "";

        logger.LogInformation("Path: " + golemPath);

        await using GolemViewModel view = args.UseDll ?
            await GolemViewModel.Load(golemPath, args.Relay, args.Mainnet) :
            await GolemViewModel.CreateStatic(golemPath, args.Relay, args.Mainnet);

        var golem = view.Golem;
        golem.PropertyChanged += new PropertyChangedHandler(logger).For(nameof(IGolem.Status));

        bool end = false;

        do
        {
            Console.WriteLine("Start/Stop/End?");
            var line = Console.ReadLine();

            switch (line)
            {
                case "Start":
                    await golem.Start();
                    break;
                case "Stop":
                    await golem.Stop();
                    break;
                case "End":
                    end = true;
                    break;

                case "Wallet":
                    var walletAddress = golem.WalletAddress;
                    golem.WalletAddress = walletAddress;
                    Console.WriteLine($"Wallet: {walletAddress}");
                    break;

                default: Console.WriteLine($"Didn't understand: {line}"); break;
            }
        } while (!end);


        Console.WriteLine("Done");
    }
}

public class PropertyChangedHandler
{

    public PropertyChangedHandler(ILogger logger)
    {
        this.logger = logger;
    }

    readonly ILogger logger;
    public PropertyChangedEventHandler For(string name)
    {
        switch (name)
        {
            case "Status": return Status_PropertyChangedHandler;
            case "Activities": return Activities_PropertyChangedHandler;
            default: return Empty_PropertyChangedHandler;
        }
    }

    private void Status_PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is Golem.Golem golem && e.PropertyName != "Status")
            logger.LogInformation($"Status property has changed: {e.PropertyName} to {golem.Status}");
    }

    private void Activities_PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is Golem.Golem golem && e.PropertyName != "Activities")
            logger.LogInformation($"Activities property has changed: {e.PropertyName}. Current job: {golem.CurrentJob}");
    }

    private void Empty_PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        logger.LogInformation($"Property {e} is not supported in this context");
    }
}
