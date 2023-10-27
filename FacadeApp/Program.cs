using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using GolemLib;
using GolemLib.Types;

namespace FacadeApp
{
    public class FacadeAppArguments
    {
        [Option('g', "golem", Required = true, HelpText = "Path to a folder with golem executables")]
        public string? GolemPath { get; set; }
    }

    internal class Program
    {
        static async Task Main(string[] args)
        {
            string golemPath = "";

            Parser.Default.ParseArguments<FacadeAppArguments>(args)
               .WithParsed<FacadeAppArguments>(o =>
               {
                   golemPath = o.GolemPath ?? "";
               });

            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem.Golem(golemPath);

            golem.PropertyChanged += PropertyChangedHandler.For(nameof(IGolem.Status));

            await golem.StartYagna();

            await golem.StopYagna();

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
            if (sender is not Golem.Golem || e.PropertyName != "Status")
                throw new NotSupportedException($"Type or {e.PropertyName} is not supported in this context");
            var golem = sender as Golem.Golem;

            Console.WriteLine($"Property has changed: {e.PropertyName} to {golem.Status}");
        }
    }
}

