using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace Golem.IntegrationTests.Tools
{
    public class PackageBuilder
    {
        const string GITHUB_RELEASE_URL = "https://github.com/{2}/releases/download/{0}/{3}-{1}-{0}";
        const string CURRENT_GOLEM_VERSION = "pre-rel-v0.13.1-rc3";
        const string CURRENT_RUNTIME_VERSION = "pre-rel-v0.1.0-rc14";

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

            await DownloadExtractPackage(BinariesDir(dir), "golem-provider", "golemfactory/yagna", CURRENT_GOLEM_VERSION, system);
            await DownloadExtractPackage(ExeUnitsDir(dir), "runtime", "golemfactory/ya-runtime-ai", CURRENT_RUNTIME_VERSION, system);
            await DownloadExtractPackage(ExeUnitsDir(dir), "dummy-framework", "golemfactory/ya-runtime-ai", CURRENT_RUNTIME_VERSION, system);

            Directory.SetCurrentDirectory(dir);

            return dir;
        }

        static async Task DownloadExtractPackage(string dir, string artifact, string repo, string tag, string system = "windows")
        {
            var builds = await DownloadArtifact(dir, artifact, tag, repo, system);
            var extract_to = Path.GetDirectoryName(builds) ?? "";


            Extract(builds, extract_to);

            // Double ChangeExtension call to get rid of tar.gz
            var extract_package_dir = Path.ChangeExtension(Path.ChangeExtension(builds, null), null);
            CopyFilesRecursively(extract_package_dir, dir);
            Directory.Delete(extract_package_dir, true);
        }

        public static void BuildDirectoryStructure(string gamerhash_dir)
        {
            Directory.CreateDirectory(BinariesDir(gamerhash_dir));
            Directory.CreateDirectory(ProviderDataDir(gamerhash_dir));
            Directory.CreateDirectory(YagnaDataDir(gamerhash_dir));
            Directory.CreateDirectory(ExeUnitsDir(gamerhash_dir));
        }

        // Based on: https://stackoverflow.com/a/3822913
        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
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

        static void Extract(string file, string target_dir)
        {
            var ext = Path.GetExtension(file);
            switch (ext)
            {
                case ".zip":
                    ZipFile.ExtractToDirectory(file, target_dir);
                    break;
                case ".gz":
                    ExtractTGZ(file, target_dir);
                    break;
                default:
                    throw new Exception("Uknonwn archive format. File: " + file);
            }

            File.Delete(file);
        }

        // Based on: https://stackoverflow.com/a/52200001
        static void ExtractTGZ(String gzArchiveName, String destFolder)
        {
            Stream inStream = File.OpenRead(gzArchiveName);
            Stream gzipStream = new GZipInputStream(inStream);

            TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream, Encoding.UTF8);
            tarArchive.ExtractContents(destFolder);
            tarArchive.Close();

            gzipStream.Close();
            inStream.Close();
        }


        static async Task<string> DownloadArtifact(string target_dir, string artifact, string tag, string repository, string system = "windows")
        {
            var ext = system is "windows" ? ".zip" : ".tar.gz";
            var url = String.Format(GITHUB_RELEASE_URL, tag, system, repository, artifact);
            url += ext;

            Console.WriteLine(String.Format("Download URL: {0}", url));
            return await Download(target_dir, url);
        }



    }


}