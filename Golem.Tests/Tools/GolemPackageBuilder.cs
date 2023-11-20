using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace Golem.IntegrationTests.Tools
{
    public class PackageBuilder
    {
        const string CURRENT_GOLEM_VERSION = "pre-rel-v0.13.1-rc4";
        const string CURRENT_RUNTIME_VERSION = "pre-rel-v0.1.0-rc14";

        internal static string InitTestDirectory(string name)
        {
            var dir = TestDir(name);
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

        public async static Task<string> BuildRequestorDirectory(string test_name)
        {
            var dir = InitTestDirectory(String.Format("{0}_requestor", test_name));
            var system = System();

            Directory.CreateDirectory(BinariesDir(dir));
            Directory.CreateDirectory(YagnaDataDir(dir));

            await DownloadExtractPackage(BinariesDir(dir), "golem-requestor", "golemfactory/yagna", CURRENT_GOLEM_VERSION, system);

            return dir;
        }

        static async Task DownloadExtractPackage(string dir, string artifact, string repo, string tag, string system = "windows")
        {
            var builds = await DownloadArchiveArtifact(dir, artifact, tag, repo, system);
            var extract_to = Path.GetDirectoryName(builds) ?? "";

            Extract(builds, extract_to);

            var extract_package_dir = Path.ChangeExtension(builds, null);
            if (Path.GetExtension(extract_package_dir) == ".tar")
            {
                extract_package_dir = Path.ChangeExtension(extract_package_dir, null);
            }

            SetPermissions(extract_package_dir);

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

        internal static string TestDir(string name)
        {
            var build_dir = AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? Path.GetTempPath();
            var tests_dir = Path.Combine(build_dir, "../../../tests");
            return Path.GetFullPath(Path.Combine(tests_dir, name));
        }

        internal static string BinariesDir(string gamerhash_dir)
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

        internal static async Task<string> Download(string target_dir, string url)
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
                    var extract_dir_name = Path.GetFileNameWithoutExtension(file);
                    var extract_package_dir = Path.Combine(target_dir, extract_dir_name);
                    ZipFile.ExtractToDirectory(file, extract_package_dir);
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

        public static void SetFilePermissions(string fileName)
        {
            // Get a FileSecurity object that represents the
            // current security settings.
            var file = new FileInfo(fileName);
            if (OperatingSystem.IsWindows())
            {
                FileSecurity fSecurity = FileSystemAclExtensions.GetAccessControl(file);
                fSecurity.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.ExecuteFile, AccessControlType.Allow));
                FileSystemAclExtensions.SetAccessControl(file, fSecurity);
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                file.UnixFileMode |= UnixFileMode.UserExecute | UnixFileMode.OtherExecute | UnixFileMode.GroupExecute;
            }
        }

        public static void SetPermissions(string directory)
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                SetFilePermissions(file);
            }
        }


        static async Task<string> DownloadArchiveArtifact(string target_dir, string artifact, string tag, string repository, string system = "windows")
        {
            var ext = system is "windows" ? ".zip" : ".tar.gz";
            var url = String.Format("https://github.com/{2}/releases/download/{0}/{3}-{1}-{0}", tag, system, repository, artifact);
            url += ext;

            Console.WriteLine(String.Format("Download archive: {0}", url));
            return await Download(target_dir, url);
        }
    }


}
