using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Medallion.Shell;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Golem.Yagna
{
    public class OutputLogger : TextWriter
    {
        public OutputLogger(ILogger? logger, string prefix, LogLevel lvl = LogLevel.Information)
        {
            _logger = logger ?? NullLogger.Instance;
            _lvl = lvl;
            _prefix = prefix;
            _strBuilder = new StringBuilder();
        }

        private readonly ILogger _logger;
        private readonly LogLevel _lvl;
        private readonly string _prefix;
        private StringBuilder _strBuilder;
        private readonly object _strBuilderLock = new object();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (value == '\n')
            {
                WriteLine();
            }
            else
            {
                lock (_strBuilderLock)
                {
                    _strBuilder.Append(value);
                }
            }
        }

        public override void WriteLine()
        {
            string line;
            lock (_strBuilderLock)
            {
                line = _strBuilder.ToString();
                _strBuilder = new StringBuilder();
            }
            _logger.Log(_lvl, $"{_prefix}: {line}");
        }
    }

    public class ProcessFactory
    {
        public static Command CreateProcess<OUT_WRITER, ERR_WRITER>(string executable, IEnumerable<object>? args, Dictionary<string, string> env, OUT_WRITER stdOut, ERR_WRITER errOut)
        where OUT_WRITER : TextWriter
        where ERR_WRITER : TextWriter
        {
            var argList = args?.ToList();
            argList?.RemoveAll(s => string.IsNullOrWhiteSpace((string?)s));
            var executablePath = Path.GetFullPath(executable);
            var workDir = Directory.GetParent(executablePath)?.ToString() ?? "";

            return Command
                .Run(executablePath, argList, options => updateOptions(options, workDir, env))
                .RedirectTo(stdOut)
                .RedirectStandardErrorTo(errOut);
        }

        static Shell.Options updateOptions(Shell.Options options, string workDir, Dictionary<string, string> env)
        {
            options = options
                .EnvironmentVariables(env)
                .WorkingDirectory(workDir)
                .ThrowOnError(true)
                .DisposeOnExit(false)
                .StartInfo(info =>
                {
                    info.CreateNoWindow = true;
                });
            return options;
        }

        public static Process CreateNativeProcess<OUT_WRITER, ERR_WRITER>(string fileName, IEnumerable<object>? args, Dictionary<string, string> env, OUT_WRITER stdOut, ERR_WRITER errOut)
        where OUT_WRITER : TextWriter
        where ERR_WRITER : TextWriter
        {
            // List<string> args2 = new List<string>();
            // args2.Append("/c");
            // args2.Append("D:\\Code\\gamerhash-facade\\modules\\golem\\ya-provider.exe");
            // foreach (var a in args) {
            //     args2.Append(a);
            // };
            // fileName = "cmd";
            // var startInfo = CreateProcessStartInfo(fileName, args2);

            var startInfo = CreateProcessStartInfo(fileName, args);

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
                EnableRaisingEvents = true,
            };

            return process;
        }

        private static ProcessStartInfo CreateProcessStartInfo(string fileName, IEnumerable<object>? args)
        {
            var startInfo = CreateProcessStartInfo(fileName);
            foreach (var arg in args) {
                var arg1 = arg.ToString();
                startInfo.ArgumentList.Add(arg1);
            }

            return startInfo;
        }

        private static ProcessStartInfo CreateProcessStartInfo(string fileName)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                // Error: The Process object must have the UseShellExecute property set to false in order to use environment variables.
                // UseShellExecute = true,

                // RedirectStandardOutput = false,
                // RedirectStandardError = false,
                // RedirectStandardInput = false,

                // RedirectStandardOutput = true,
                // RedirectStandardError = true,
                // RedirectStandardInput = false,

                // CreateNoWindow = false,


                
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                // RedirectStandardInput = false,
                CreateNoWindow = true,

                // WindowStyle = ProcessWindowStyle.Hidden,

                
            };
            return startInfo;
        }

        private static void BindOutputEventHandlers(Process proc)
        {
            proc.OutputDataReceived += OnOutputDataRecv;
            proc.ErrorDataReceived += OnErrorDataRecv;
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();
        }

        private static void OnOutputDataRecv(object sender, DataReceivedEventArgs e)
        {
            // _logger.LogInformation($"{e.Data}");
            Console.WriteLine($">>> {e.Data}");
        }
        private static void OnErrorDataRecv(object sender, DataReceivedEventArgs e)
        {
            // _logger.LogInformation($"{e.Data}");
            Console.WriteLine($">>> {e.Data}");
        }



        static Process providerProc = null;

        public static Command CreateProcessAlt<OUT_WRITER, ERR_WRITER>(string executable, IEnumerable<object>? args, Dictionary<string, string> env, OUT_WRITER stdOut, ERR_WRITER errOut, ILogger logger)
        where OUT_WRITER : TextWriter
        where ERR_WRITER : TextWriter
        {
            
            // var executablePath = Path.GetFullPath(executable);
            // var workDir = Directory.GetParent(executablePath)?.ToString() ?? "";
            // var dotEnv = Path.Combine(workDir, ".env");
            // if (Path.Exists(dotEnv)) {
            //     File.Delete(dotEnv);
            // }
            // var fs = new StreamWriter(dotEnv);
            // foreach(KeyValuePair<string, string> entry in env)
            // {
            //     fs.WriteLine($"{entry.Key}={entry.Value}");
            // }
            // fs.Close();

            // executable = Path.ChangeExtension(executable, ".bat");

            logger.LogInformation($"XXX Creting process w exec {executable}");
            providerProc = CreateNativeProcess(executable, args, env, stdOut, errOut);
            if (!providerProc.Start()) {
                throw new GolemException("Failed to start Provider process");
            }

            // BindOutputEventHandlers(providerProc);
            
            logger.LogInformation($"XXX Process id {providerProc.Id}");

            // ctrc_alt(providerProc, logger).GetAwaiter().GetResult();

            // close(providerProc, logger).GetAwaiter().GetResult();

            Thread.Sleep(10_000);
            if (Command.TryAttachToProcess(providerProc.Id, out var thisCommand)) {
                ctrlc(thisCommand, logger).GetAwaiter().GetResult();
                return thisCommand;
            } else {
                logger.LogError("XXX Failed to attach to process");
            }

            Thread.Sleep(60_000);
            throw new GolemException("XXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
        }

        // #if Windows
        internal const int CTRL_C_EVENT = 0;
        [DllImport("kernel32.dll")]
        internal static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);
        // Delegate type to be used as the Handler Routine for SCCH
        delegate Boolean ConsoleCtrlDelegate(uint CtrlType);
        // #endif

        private static async Task ctrc_alt(Process proc, ILogger logger) {
            await Task.Delay(10_000);
            logger.LogInformation("XXX Sending Ctrl-C");
            try {

                // #if Linux
                //     logger.LogInformation("XXX Linux kill");
                //     proc.Kill();
                // #elif Windows
                    logger.LogInformation("XXX WIndows ctrl-c");

                    if (AttachConsole((uint)proc.Id)) {
                        SetConsoleCtrlHandler(null, true);
                        try { 
                            if (!GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
                                logger.LogError("XXX GenerateConsoleCtrlEvent failed");
                            logger.LogInformation("XXX Waiting for exit");
                            proc.WaitForExit();
                            logger.LogInformation("XXX GenerateConsoleCtrlEvent succeeded");
                        } catch (Exception e) {
                            logger.LogError("XXX GenerateConsoleCtrlEvent failed. Err {0}", e);
                        } finally {
                            SetConsoleCtrlHandler(null, false);
                            FreeConsole();
                        }
                        logger.LogInformation("XXX Ctrl c success XXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
                    } else {
                        logger.LogError("XXX AttachConsole failed");
                    }

                // #endif

            } catch (Exception e) {
                logger.LogInformation("XXX Failed to sent: {0}", e);
            }
            await Task.Delay(10_000);
        }

        private static async Task close(Process proc, ILogger logger) {
            await Task.Delay(10_000);
            logger.LogInformation("XXX Closing");
            try {
                //// Does not shutdown Yagna because it has no window. This setting does not help:
                /// ProcessStartInfo CreateNoWindow = false 
                /// only make console to not open when parent process is started as an console app
                if (!proc.CloseMainWindow()) {
                    logger.LogInformation("XXX Failed to close");
                } else {
                    logger.LogInformation("XXX Closed main windows");
                }

            } catch (Exception e) {
                logger.LogInformation("XXX Failed to close using CloseMainWindow", e);
            }
            
            try {
                //// Does not shutdown Yagna because yagna does not listen on stdin.
                proc.StandardInput.Close();
                // proc.CancelOutputRead();
                // proc.StandardOutput.Close();
                logger.LogInformation("XXX Closed using IO");
            } catch (Exception e) {
                logger.LogInformation("XXX Failed to close using IO", e);
            }
            await Task.Delay(10_000);
        }

        private static async Task ctrlc(Command cmd, ILogger logger) {
            logger.LogInformation("XXX Giving provider time to start");
            await Task.Delay(10_000);
            logger.LogInformation("XXX Sending Ctrl-C");
            if (await cmd.TrySignalAsync(CommandSignal.ControlC)) {
                logger.LogInformation("XXX Ctrl-C sent");
            } else {
                logger.LogInformation("XXX Ctrl-C failed");
            }
            await Task.Delay(10_000);
        }

        // public static Command CreateProcessAlt<OUT_WRITER, ERR_WRITER>(string executable, IEnumerable<object>? args, Dictionary<string, string> env, OUT_WRITER stdOut, ERR_WRITER errOut)
        // where OUT_WRITER : TextWriter
        // where ERR_WRITER : TextWriter
        // {
        //     var argList = args?.ToList();
        //     argList?.RemoveAll(s => string.IsNullOrWhiteSpace((string?)s));
        //     var executablePath = Path.GetFullPath(executable);
        //     var workDir = Directory.GetParent(executablePath)?.ToString() ?? "";

        //     Shell MyShell = new Shell(options => options
        //         .EnvironmentVariables(env)
        //         .WorkingDirectory(workDir)
        //         // .ThrowOnError(true)
        //         // .DisposeOnExit(false)
        //         .StartInfo(info =>
        //         {
        //             // info.CreateNoWindow = false;
        //             info.UseShellExecute = true;
        //             info.WindowStyle = ProcessWindowStyle.Hidden;
        //             // info.RedirectStandardOutput = false;
        //             // info.RedirectStandardError = false;
        //             // info.RedirectStandardInput = false;
        //         }));

        //     return MyShell.Run(executablePath, argList);
        //     // return Command
        //     //     .Run(executablePath, argList, options => updateOptionsAlt(options, workDir, env))
        //     //     // .RedirectTo(stdOut)
        //     //     // .RedirectStandardErrorTo(errOut)
        //     //     ;
        // }

        // static Shell.Options updateOptionsAlt(Shell.Options options, string workDir, Dictionary<string, string> env)
        // {
        //     options = options
        //         .EnvironmentVariables(env)
        //         .WorkingDirectory(workDir)
        //         .ThrowOnError(true)
        //         .DisposeOnExit(false)
        //         .StartInfo(info =>
        //         {
        //             info.CreateNoWindow = false;
        //             info.UseShellExecute = true;
        //         });
        //     return options;
        // }


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

        // TODO instead of passing logger as param and keeping `cmd` as variable, keep ProcessFactory (with `cmd` and `logger`) as a variable.
        public static async Task<int> StopCmd(Command cmd, int stopTimeoutMs = 30_000, ILogger? logger = null)
        {
            if (!cmd.Process.HasExited)
            {
                if (!await cmd.TrySignalAsync(CommandSignal.ControlC))
                {
                    logger?.LogWarning("Failed to signal Ctrl-C to process. Killing it.");
                    cmd.Kill();
                }
                CancellationTokenSource stopTimeoutTokenSrc = new CancellationTokenSource();
                var stopTimeoutToken = stopTimeoutTokenSrc.Token;
                stopTimeoutTokenSrc.CancelAfter(stopTimeoutMs);
                try
                {
                    logger?.LogInformation("Waiting for process to stop.");
                    await cmd.Process.WaitForExitAsync(stopTimeoutToken);
                    logger?.LogInformation("Process stopped.");
                }
                catch (TaskCanceledException err)
                {
                    logger?.LogWarning($"Failed to stop process. Killing it. Err: {err.Message}");
                    cmd.Kill();
                }
            } else {
                logger?.LogWarning("Process has exited already.");
            }
            return cmd.Process.ExitCode;
        }
    }

    interface IGolemException {}

    public class GolemProcessException : Exception, IGolemException
    {

        public GolemProcessException(string message)
            : base(message)
        {
        }
    }

    public class GolemException : Exception, IGolemException
    {
        public GolemException(string message)
            : base(message)
        {
        }
    }
}
