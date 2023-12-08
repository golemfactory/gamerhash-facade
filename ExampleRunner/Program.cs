using App;

using CommandLine;

using Microsoft.Extensions.Logging;


public class AppArguments
{
    [Option('g', "golem", Required = true, HelpText = "Path to a folder with golem executables (modules)")]
    public string? GolemPath { get; set; }
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

        var App = new FullExample(workDir, "Requestor1", loggerFactory);
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

        Console.TreatControlCAsInput = true;
        logger.LogInformation("Press Ctrl+C To Terminate");

        ConsoleKeyInfo cki;
        do
        {
            cki = Console.ReadKey();

            if (((cki.Modifiers & ConsoleModifiers.Control) != 0) && (cki.Key == ConsoleKey.C))
            {
                logger.LogInformation("Captured Ctrl-C. Finishing example...");
                logger.LogInformation("Press Ctrl+X To Kill application.");

                // _ = Task.Run(async () =>
                // {
                //     await App.Stop();
                //     logger.LogInformation("App shutdown");
                // });

                App.Stop().Wait(10000);
                logger.LogInformation("App finished");
                break;
            }
            // else if (((cki.Modifiers & ConsoleModifiers.Control) != 0) && (cki.Key == ConsoleKey.X))
            // {
            //     logger.LogInformation("Captured Ctrl-X. Killing...");
            //     App.Kill().Wait(100);
            //     break;
            // }
        } while (true);
    }
}



