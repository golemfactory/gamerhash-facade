using App;

using CommandLine;

using Golem;

using Golem.Tools;

using Microsoft.Extensions.Logging;


public enum Framework
{
    Automatic,
    Dummy,
}

public class AppArguments
{
    [Option('g', "golem", Required = true, HelpText = "Path to a folder with golem executables (modules)")]
    public string? GolemPath { get; set; }
    [Option('r', "relay", Default = RelayType.Central, Required = false, HelpText = "Change relay to devnet yacn2a, local or central net")]
    public required RelayType Relay { get; set; }
    [Option('f', "framework", Default = Framework.Automatic, Required = false, HelpText = "Type of AI Framework to run")]
    public required Framework AiFramework { get; set; }
    [Option('m', "mainnet", Default = false, Required = false, HelpText = "Enables usage of mainnet")]
    public required bool Mainnet { get; set; }
    [Option('p', "pay-interval", Default = null, Required = false, HelpText = "Interval between partial payments in seconds")]
    public UInt32? PaymentInterval { get; set; }
}


class ExampleRunner
{
    static void Main(string[] args)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddSimpleConsole()
        );

        var parsed = Parser.Default.ParseArguments<AppArguments>(args).Value;
        var workDir = parsed.GolemPath ?? "";

        NetConfig.SetEnv(parsed.Relay);

        var App = new FullExample(workDir, "Requestor", loggerFactory, runtime: parsed.AiFramework.ToString().ToLower(), parsed.Mainnet);

        App.PaymentInterval = parsed.PaymentInterval;
        var logger = loggerFactory.CreateLogger("Example");

        _ = Task.Run(async () =>
        {
            try
            {
                await App.Run();
                await App.WaitForFinish();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error starting app: " + e.ToString());
            }
        });

        logger.LogInformation("Press Ctrl+C To Terminate");

        ConsoleHelper.WaitForCtrlC();

        Task[] tasks = new Task[2];
        tasks[0] = Task.Run(() =>
        {
            logger.LogInformation("Captured Ctrl-C. Finishing example...");
            var success = App.Stop().Wait(30000);
            logger.LogInformation("Example application finished");
        });

        tasks[1] = Task.Run(() =>
        {
            ConsoleHelper.WaitForCtrlC();

            logger.LogInformation("Captured second Ctrl-C. Killing...");
            App.Kill().Wait(100);
            logger.LogInformation("Example application killed");
        });

        Task.WaitAny(tasks);
    }
}
