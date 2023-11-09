using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;

namespace Golem.IntegrationTests.Tools
{
    public class PackageBuilder
    {
        const string GITHUB_RELEASE_URL = "https://github.com/{2}/releases/download/{0}/golem-provider-{1}-{0}";
        const string CURRENT_GOLEM_VERSION = "pre-rel-v0.13.1-rc3";
        const string CURRENT_RUNTIME_VERSION = "pre-rel-v0.1.0-rc6";

        public static string InitTestDirectory(string name)
        {
            var build_dir = AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? Path.GetTempPath();
            var tests_dir = Path.Combine(build_dir, "../../../tests");
            var dir = Path.GetFullPath(Path.Combine(tests_dir, name));

            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
            Directory.CreateDirectory(BinariesDir(dir));
            return dir;
        }

        public async static Task<string> BuildTestDirectory(string test_name)
        {
            var dir = InitTestDirectory(test_name);
            var system = System();
            BuildDirectoryStructure(dir);

            var builds = await DownloadArtifact(BinariesDir(dir), CURRENT_GOLEM_VERSION, "golemfactory/yagna", system);
            var extract_to = Path.GetDirectoryName(builds) ?? "";

            ZipFile.ExtractToDirectory(builds, extract_to);
            return dir;
        }

        public static void BuildDirectoryStructure(string gamerhash_dir)
        {
            Directory.CreateDirectory(BinariesDir(gamerhash_dir));
            Directory.CreateDirectory(ProviderDataDir(gamerhash_dir));
            Directory.CreateDirectory(YagnaDataDir(gamerhash_dir));
            Directory.CreateDirectory(ExeUnitsDir(gamerhash_dir));
        }

        public static string System()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "windows";
            }
            else
            {
                throw new Exception("System not supported.");
            }
        }

        public static string BinariesDir(string gamerhash_dir)
        {
            return Path.Combine(gamerhash_dir, "modules/golem");
        }

        public static string DataDir(string gamerhash_dir)
        {
            return Path.Combine(gamerhash_dir, "modules/golem-data");
        }

        public static string ProviderDataDir(string gamerhash_dir)
        {
            return Path.Combine(DataDir(gamerhash_dir), "provider");
        }

        public static string YagnaDataDir(string gamerhash_dir)
        {
            return Path.Combine(DataDir(gamerhash_dir), "yagna");
        }

        public static string ExeUnitsDir(string gamerhash_dir)
        {
            return Path.Combine(gamerhash_dir, "modules/plugins");
        }

        static async Task<string> Download(string target_dir, string url)
        {
            var name = Path.GetFileName(url);
            var target_file = Path.Combine(target_dir, name);
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    using (var fs = new FileStream(target_file, FileMode.CreateNew))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }
                else
                {
                    throw new Exception("Failed to download: " + response.ToString());
                }

            }
            return target_file;
        }

        static async Task<string> DownloadArtifact(string target_dir, string tag, string repository, string system = "windows")
        {
            var url = String.Format(GITHUB_RELEASE_URL, tag, system, repository);
            url += system is "windows" ? ".zip" : ".tar.gz";

            Console.WriteLine(String.Format("Download URL: {0}", url));
            return await Download(target_dir, url);
        }
    }


}