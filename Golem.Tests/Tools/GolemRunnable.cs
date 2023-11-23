
using System.Diagnostics;

using Golem.Yagna;

namespace Golem.IntegrationTests.Tools
{
    public abstract class GolemRunnable
    {

        protected string _dir;
        private Process? _golemProcess;

        protected GolemRunnable(string dir)
        {
            _dir = dir;
        }

        public abstract bool Start();

        protected bool StartProcess(string file_name, string working_dir, string args, Dictionary<string, string> env)
        {
            var process = CreateProcess(file_name, args, env, true);
            process.StartInfo.WorkingDirectory = working_dir;
            GolemRunnable.AddShutdownHook(process);
            if (process.Start())
            {
                _golemProcess = process;
                return !_golemProcess.HasExited;
            }
            _golemProcess = null;
            return false;
        }

        protected Process CreateProcess(string file_name, string args, Dictionary<string, string> env, bool openConsole = true)
        {
            var file_name_w_ext = ProcessFactory.BinName(file_name);
            var dir = Path.GetFullPath(_dir);
            var runnable_path = Path.Combine(dir, "modules", "golem", file_name_w_ext);
            return ProcessFactory.CreateProcess(runnable_path, args, openConsole, env);
        }

        public async Task Stop()
        {
            if (_golemProcess == null || _golemProcess.HasExited)
                return;

            _golemProcess.Kill(true);
            await _golemProcess.WaitForExitAsync();
            _golemProcess = null;
        }

        protected static async Task<string> DownloadBinaryArtifact(string target_dir, string artifact, string tag, string repository)
        {
            var ext = ExecutableFileExtension();
            var url = String.Format("https://github.com/{1}/releases/download/{0}/{2}", tag, repository, artifact);
            url += ext;

            Console.WriteLine(String.Format("Download binary: {0}", url));
            return await PackageBuilder.Download(target_dir, url);
        }

        public static String ExecutableFileExtension()
        {
            return OperatingSystem.IsWindows() ? ".exe" : "";
        }

        public static void AddShutdownHook(Process childProcess)
        {
            AppDomain.CurrentDomain.DomainUnload += (obj, eventArgs) => { childProcess.Kill(); childProcess.WaitForExit(); };
            AppDomain.CurrentDomain.ProcessExit += (obj, eventArgs) => { childProcess.Kill(); childProcess.WaitForExit(); };
            AppDomain.CurrentDomain.UnhandledException += (obj, eventArgs) => { childProcess.Kill(); childProcess.WaitForExit(); };
        }

    }
}
