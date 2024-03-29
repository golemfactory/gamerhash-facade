using System.Reflection;

using Golem.Tools;

using GolemLib;

using Microsoft.Extensions.Logging;

namespace Golem.Tests
{
    public class TestUtils
    {
        public async static Task<IGolem> LoadBinaryLib(string dllPath, string modulesDir, ILoggerFactory loggerFactory)
        {
            const string factoryType = "Golem.Factory";

            Assembly ass = Assembly.LoadFrom(dllPath);
            Type? t = ass.GetType(factoryType) ?? throw new Exception("Factory Type not found. Lib not loaded: " + dllPath);
            var obj = Activator.CreateInstance(t) ?? throw new Exception("Creating Factory instance failed. Lib not loaded: " + dllPath);
            var factory = obj as IFactory ?? throw new Exception("Cast to IFactory failed.");
            return await factory.Create(modulesDir, loggerFactory, false);
        }

        public async static Task<IGolem> Golem(string golemPath, ILoggerFactory loggerFactory) {
            var modulesDir = PackageBuilder.ModulesDir(golemPath);
            return await new Factory().Create(modulesDir, loggerFactory, false);
        }
    }
}
