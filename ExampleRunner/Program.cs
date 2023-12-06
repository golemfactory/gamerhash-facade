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
        MainAsync(args).Wait();

    }

    static async Task MainAsync(string[] args)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddSimpleConsole()
        );

        var parsed = Parser.Default.ParseArguments<AppArguments>(args).Value;
        var workDir = parsed.GolemPath ?? "";

        try
        {
            var App = new FullExample(workDir, "Requestor1", loggerFactory);

            Console.CancelKeyPress += async (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                await App.Stop();
            };


            await App.Run();
            await App.WaitForFinish();
        }
        catch (Exception e)
        {
            Console.WriteLine("Error starting app: " + e.ToString());
        }
    }
}



