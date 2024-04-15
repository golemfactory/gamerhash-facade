using System.ComponentModel;
using System.Reflection;
using System.Threading.Channels;

using Golem.Tools;

using GolemLib;

using Microsoft.Extensions.Logging;

namespace Golem.Tests
{
    public class TestUtils
    {
        public async static Task<IGolem> LoadBinaryLib(string dllPath, string modulesDir, ILoggerFactory loggerFactory, string? dataDir = null)
        {
            const string factoryType = "Golem.Factory";

            Assembly ass = Assembly.LoadFrom(dllPath);
            Type? t = ass.GetType(factoryType) ?? throw new Exception("Factory Type not found. Lib not loaded: " + dllPath);
            var obj = Activator.CreateInstance(t) ?? throw new Exception("Creating Factory instance failed. Lib not loaded: " + dllPath);
            var factory = obj as IFactory ?? throw new Exception("Cast to IFactory failed.");
            return await factory.Create(modulesDir, loggerFactory, false, dataDir);
        }

        public async static Task<IGolem> Golem(string golemPath, ILoggerFactory loggerFactory, string? dataDir = null) {
            var modulesDir = PackageBuilder.ModulesDir(golemPath);
            return await new Factory().Create(modulesDir, loggerFactory, false, dataDir);
        }

        /// <summary>
        /// Waits for file up to `timeoutSec` and reads it as a text file. Throws Exception on timeout.
        /// </summary>
        public async static Task<String> WaitForFileAndRead(String path, int timeoutSec = 15) {
            int i = 0;
            while (!File.Exists(path) && i < timeoutSec)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                i++;
            }
            if (i == timeoutSec)
                throw new Exception($"File {path} was not created");
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Creates channel of updated properties.
        /// `extraHandler` is invoked each time property arrives.
        /// </summary>
        public static Channel<T?> PropertyChangeChannel<OBJ, T>(OBJ? obj, string propName, ILoggerFactory loggerFactory, Action<T?>? extraHandler = null) where OBJ : INotifyPropertyChanged
        {
            var eventChannel = Channel.CreateUnbounded<T?>();
            Action<T?> emitEvent = async (v) =>
            {
                extraHandler?.Invoke(v);
                await eventChannel.Writer.WriteAsync(v);
            };

            if (obj != null)
                obj.PropertyChanged += new PropertyChangedHandler<OBJ, T>(propName, emitEvent, loggerFactory).Subscribe();

            return eventChannel;
        }

        
        /// <summary>
        /// Reads from `channel` and returns first `T` for which `matcher` returns `false`
        /// </summary>
        /// <exception cref="Exception">Thrown when reading channel exceeds in total `timeoutMs`</exception>
        public async static Task<T> ReadChannel<T>(ChannelReader<T> channel, Func<T, bool>? matcher = null, double timeoutMs = 10_000, ILogger? logger = null)
        {
            var cancelTokenSource = new CancellationTokenSource();
            cancelTokenSource.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
            static bool FalseMatcher(T x) => false;
            matcher ??= FalseMatcher;
            while (await channel.WaitToReadAsync(cancelTokenSource.Token))
            {
                if (channel.TryRead(out var value) && value is T tValue && !matcher.Invoke(tValue))
                {
                    return tValue;
                }
                else
                {
                    logger?.LogInformation($"Skipping element: {value}");
                }
            }

            throw new Exception($"Failed to find matching {nameof(T)} within {timeoutMs} ms.");
        }
    }
}
