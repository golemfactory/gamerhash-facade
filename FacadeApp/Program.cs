using System.ComponentModel;
using System.Diagnostics;

using CommandLine;

using Golem;

using GolemLib;
using Microsoft.Extensions.Logging;

namespace FacadeApp
{
    public class FacadeAppArguments
    {
        [Option('g', "golem", Required = true, HelpText = "Path to a folder with golem executables")]
        public string? GolemPath { get; set; }
        [Option('d', "data_dir", Required = false, HelpText = "Path to the provider's data directory")]
        public string? DataDir { get; set; }
        [Option('m', "mainnet", Default = false, Required = false, HelpText = "Enables usage of mainnet")]
        public required bool Mainnet { get;  set; }
    }


    internal class Program
    {
        static async Task Main(string[] args)
        {
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
               builder.AddSimpleConsole()
            );

            var logger = loggerFactory.CreateLogger<Program>();

            string golemPath = "";
            string? dataDir = null;
            bool mainnet = false;

            Parser.Default.ParseArguments<FacadeAppArguments>(args)
               .WithParsed<FacadeAppArguments>(o =>
               {
                   golemPath = o.GolemPath ?? "";
                   dataDir = o.DataDir;
                   mainnet = o.Mainnet;
               });

            logger.LogInformation("Path: " + golemPath);
            logger.LogInformation("DataDir: " + (dataDir ?? ""));

            var binaries = Path.Combine(golemPath, "golem");
            dataDir = Path.Combine(golemPath, "golem-data");
;
            var golem = await new Factory().Create(golemPath, loggerFactory, mainnet);

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
}
