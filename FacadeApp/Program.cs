using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
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
        static void Main(string[] args)
        {
            string golemPath = "";

            Parser.Default.ParseArguments<FacadeAppArguments>(args)
               .WithParsed<FacadeAppArguments>(o =>
               {
                   golemPath = o.GolemPath ?? "";
               });

            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem.Golem(golemPath);

            golem.StartYagna();

            Console.WriteLine("Done");
        }
    }
}

