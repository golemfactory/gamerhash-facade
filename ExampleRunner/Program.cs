using App;

using CommandLine;

using Microsoft.Extensions.Logging;


public class AppArguments
{
    [Option('g', "golem", Required = true, HelpText = "Path to a folder with golem executables (modules)")]
    public string? GolemPath { get; set; }
    [Option('r', "devnet-relay", Default = false, Required = false, HelpText = "Change relay to devnet yacn2a")]
    public required bool DevnetRelay { get; set; }
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

        if (parsed.DevnetRelay)
            System.Environment.SetEnvironmentVariable("YA_NET_RELAY_HOST", "yacn2a.dev.golem.network:7477");

        var App = new FullExample(workDir, "Requestor", loggerFactory);
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

        waitForCtrlC();

        Task[] tasks = new Task[2];
        tasks[0] = Task.Run(() =>
        {
            logger.LogInformation("Captured Ctrl-C. Finishing example...");
            var success = App.Stop().Wait(10000);
            logger.LogInformation("Example application finished");
        });

        tasks[1] = Task.Run(() =>
        {
            waitForCtrlC();

            logger.LogInformation("Captured second Ctrl-C. Killing...");
            App.Kill().Wait(100);
            logger.LogInformation("Example application killed");
        });

        Task.WaitAny(tasks);
    }

    static void waitForCtrlC()
    {
        Console.TreatControlCAsInput = true;

        ConsoleKeyInfo cki;
        do
        {
            cki = Console.ReadKey();
        } while (!(((cki.Modifiers & ConsoleModifiers.Control) != 0) && (cki.Key == ConsoleKey.C)));
    }
}



