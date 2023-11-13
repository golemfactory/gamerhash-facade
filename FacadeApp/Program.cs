using System.ComponentModel;
using CommandLine;
using GolemLib;

namespace FacadeApp
{
    public class FacadeAppArguments
    {
        [Option('g', "golem", Required = true, HelpText = "Path to a folder with golem executables")]
        public string? GolemPath { get; set; }
        [Option('d', "data_dir", Required = false, HelpText = "Path to the provider's data directory")]
        public string? DataDir { get; set; }
    }



    internal class Program
    {
        static async Task Main(string[] args)
        {
            string golemPath = "";
            string? dataDir = null;

            Parser.Default.ParseArguments<FacadeAppArguments>(args)
               .WithParsed<FacadeAppArguments>(o =>
               {
                   golemPath = o.GolemPath ?? "";
                   dataDir = o.DataDir;
               });

            Console.WriteLine("Path: " + golemPath);
            Console.WriteLine("DataDir: " + (dataDir ?? ""));

            await using (var golem = new Golem.Golem(golemPath, dataDir))
            {

                golem.PropertyChanged += PropertyChangedHandler.For(nameof(IGolem.Status));


                bool end = false;

                do
                {
                    Console.WriteLine("Star/Stop/End?");
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
            }

            Console.WriteLine("Done");
        }
    }

    public class PropertyChangedHandler
    {
        public static PropertyChangedEventHandler For(string name)
        {
            switch(name)
            {
                case "Status": return Status_PropertyChangedHandler;
            }
            throw new NotSupportedException($"PropertyChangedHandler not implemented for {name}");
        }

        private static void Status_PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not Golem.Golem golem || e.PropertyName != "Status")
                throw new NotSupportedException($"Type or {e.PropertyName} is not supported in this context");

            Console.WriteLine($"Property has changed: {e.PropertyName} to {golem.Status}");
        }
    }
}



