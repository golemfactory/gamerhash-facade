
using System.Diagnostics;
using System.Runtime.InteropServices;

using Golem.Yagna;

using Medallion.Shell;

namespace Golem.IntegrationTests.Tools
{
    public abstract class GolemRunnable
    {
        // Delegate type to be used as the Handler Routine for SCCH
        delegate Boolean ConsoleCtrlDelegate(uint CtrlType);

        protected string _dir;
        private Command? _golemProcess;

        protected GolemRunnable(string dir)
        {
            _dir = dir;
        }

        public abstract bool Start();

        protected bool StartProcess(string file_name, string working_dir, string args, Dictionary<string, string> env, bool openConsole = true)
        {
            Command process = RunCommand(file_name, working_dir, args, env);
            // process.StartInfo.WorkingDirectory = working_dir;
            GolemRunnable.AddShutdownHook(process);
            if (!process.Process.HasExited)
            {
                _golemProcess = process;
                return !_golemProcess.Process.HasExited;
            }

            Console.WriteLine("Command stopped. Output:\n{0}", String.Join("\n", _golemProcess.GetOutputAndErrorLines()));
            _golemProcess = null;
            return false;
        }

        protected Command RunCommand(string file_name, string working_dir, string args, Dictionary<string, string> env)
        {
            var file_name_w_ext = ProcessFactory.BinName(file_name);
            var dir = Path.GetFullPath(_dir);
            var runnable_path = Path.Combine(dir, "modules", "golem", file_name_w_ext);

            var args_list = args.Split(null);
            return Command.Run(runnable_path, args_list, options => options
                .EnvironmentVariables(env)
                .WorkingDirectory(working_dir)
                .ThrowOnError(true)
                .DisposeOnExit(false)
            );
        }

        public async Task Stop(StopMethod stopMethod = StopMethod.SigKill)
        {
            if (_golemProcess == null || _golemProcess.Process.HasExited)
                return;
            switch (stopMethod)
            {
                case StopMethod.SigKill:
                    _golemProcess.Kill();
                    break;
                case StopMethod.SigInt:
                    if (!await _golemProcess.TrySignalAsync(CommandSignal.ControlC))
                    {
                        Console.WriteLine("Failed to interrupt process. Killing");
                        _golemProcess.Kill();
                    }
                    break;
            }
            try
            {
                _golemProcess.Wait();
            }
            finally
            {
                _golemProcess = null;
            }
        }

        protected static async Task<string> DownloadBinaryArtifact(string artifact, string tag, string repository)
        {
            var ext = ExecutableFileExtension();
            var url = String.Format("https://github.com/{1}/releases/download/{0}/{2}", tag, repository, artifact);
            url += ext;

            Console.WriteLine(String.Format("Download binary: {0}", url));
            return await PackageBuilder.Download(url);
        }

        public static String ExecutableFileExtension()
        {
            return OperatingSystem.IsWindows() ? ".exe" : "";
        }

        public static void AddShutdownHook(Command childProcess)
        {
            AppDomain.CurrentDomain.DomainUnload += (obj, eventArgs) => { childProcess.Kill(); childProcess.Wait(); };
            AppDomain.CurrentDomain.ProcessExit += (obj, eventArgs) => { childProcess.Kill(); childProcess.Wait(); };
            AppDomain.CurrentDomain.UnhandledException += (obj, eventArgs) => { childProcess.Kill(); childProcess.Wait(); };
        }
    }

    public enum StopMethod
    {
        SigKill,
        SigInt
    }
}
