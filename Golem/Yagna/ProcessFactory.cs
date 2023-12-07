using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Golem.Yagna
{
    public class ProcessFactory
    {
        public static Process CreateProcess(string fileName, string args, bool openConsole, Dictionary<string, string> env)
        {
            var initStartInfo = () => CreateProcessStartInfo(fileName, args, openConsole);
            return CreateProcess(fileName, initStartInfo, env);
        }

        public static Process CreateProcess(string fileName, List<string> args, bool openConsole, Dictionary<string, string> env)
        {
            var initStartInfo = () => CreateProcessStartInfo(fileName, args, openConsole);
            return CreateProcess(fileName, initStartInfo, env);
        }

        private static Process CreateProcess(string fileName, Func<ProcessStartInfo> initStartInfo, Dictionary<string, string> env)
        {
            var startInfo = initStartInfo();

            foreach (var (k, v) in env)
            {
                if(startInfo.EnvironmentVariables.ContainsKey(k))
                    startInfo.EnvironmentVariables[k] = v;
                else
                    startInfo.EnvironmentVariables.Add(k, v);
            }

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            return process;
        }

        private static ProcessStartInfo CreateProcessStartInfo(string fileName, string args, bool openConsole)
        {
            var startInfo = CreateProcessStartInfo(fileName, openConsole);
            startInfo.Arguments = args;

            return startInfo;
        }

        private static ProcessStartInfo CreateProcessStartInfo(string fileName, List<string> args, bool openConsole)
        {
            var startInfo = CreateProcessStartInfo(fileName, openConsole);
            args.ForEach(startInfo.ArgumentList.Add);

            return startInfo;
        }

        private static ProcessStartInfo CreateProcessStartInfo(string fileName, bool openConsole)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false
            };

            if (openConsole)
            {
                startInfo.RedirectStandardOutput = false;
                startInfo.RedirectStandardError = false;
                startInfo.CreateNoWindow = false;
                
            }
            else
            {
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.CreateNoWindow = true;
            }

            return startInfo;
        }

        public static string BinName(string name)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.ChangeExtension(name, ".exe");
            }
            else
            {
                return Path.GetFileNameWithoutExtension(name);
            }
        }
    }
}
