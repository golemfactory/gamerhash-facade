using System.IO;
using System.IO.Compression;
using System.IO.Enumeration;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

using Newtonsoft.Json.Linq;

namespace Golem.Tools
{
    public class PackageBuilder
    {
        public static string CURRENT_GOLEM_VERSION = "v0.13.2";
        public static string CURRENT_RUNTIME_VERSION = "pre-rel-v0.1.0-rc17";

        internal static string InitTestDirectory(string name, bool cleanupData = true)
        {
            var dir = TestDir(name);
            return PrepareTestDirectory(dir, cleanupData);
        }

        public static string PrepareTestDirectory(string dir, bool cleanupData = true)
        {
            if (Directory.Exists(dir))
            {
                if (cleanupData)
                {
                    Directory.Delete(dir, true);
                }
                else
                {
                    var dataDir = Path.GetFullPath(YagnaDataDir(dir));
                    foreach (string file in Directory.EnumerateFiles(dir))
                    {
                        File.Delete(file);
                    }
                    foreach (string nestedDir in Directory.EnumerateDirectories(Path.Combine(dir, "modules")))
                    {
                        var nestedDirPath = Path.GetFullPath(Path.Combine(dir, nestedDir));
                        if (!dataDir.Equals(nestedDirPath))
                        {
                            Directory.Delete(nestedDirPath, true);
                        }
                    }
                }
            }
            Directory.CreateDirectory(BinariesDir(dir));
            return dir;
        }

        public async static Task<string> BuildTestDirectory(string test_name)
        {
            var dir = InitTestDirectory(test_name);
            var system = System();
            BuildDirectoryStructure(dir);

            await DownloadExtractPackage(BinariesDir(dir), "golem-provider", "golemfactory/yagna", CURRENT_GOLEM_VERSION);

            var exeUnitDir = ExeUnitsDir(dir);
            await DownloadExtractPackage(exeUnitDir, "runtime", "golemfactory/ya-runtime-ai", CURRENT_RUNTIME_VERSION);
            await DownloadExtractPackage(exeUnitDir, "dummy-framework", "golemfactory/ya-runtime-ai", CURRENT_RUNTIME_VERSION);

            string? dummy_descriptors = null;
            using (StreamReader r = new(Path.Combine(exeUnitDir, "ya-dummy-ai.json")))
            {
                dummy_descriptors = r.ReadToEnd();
            }
            if (dummy_descriptors != null)
            {
                var descriptors = JArray.Parse(dummy_descriptors);
                foreach (JObject descriptor in descriptors)
                {
                    var name = descriptor.GetValue("name");
                    if (name!=null && "ai".Equals(name.ToString()))
                    {
                        var runtime_name = $"ya-runtime-ai{GolemRunnable.ExecutableFileExtension()}";
                        var runtime_path = Path.Combine(exeUnitDir, runtime_name);
                        descriptor.Remove("supervisor-path");
                        descriptor.Add("supervisor-path", runtime_path);
                    }
                }
                File.WriteAllText(exeUnitDir + "/ya-dummy-ai.json", descriptors.ToString());
            }

            Directory.SetCurrentDirectory(dir);

            return dir;
        }

        public async static Task<string> BuildRequestorDirectory(string test_name, bool cleanupData = true)
        {
            var dir = InitTestDirectory(string.Format("{0}_requestor", test_name), cleanupData);
            return await BuildRequestorDirectoryRelative(dir, cleanupData);
        }

        public async static Task<string> BuildRequestorDirectoryRelative(string dir, bool cleanupData = true)
        {
            Directory.CreateDirectory(BinariesDir(dir));
            Directory.CreateDirectory(YagnaDataDir(dir));

            await DownloadExtractPackage(BinariesDir(dir), "golem-requestor", "golemfactory/yagna", CURRENT_GOLEM_VERSION);

            return dir;
        }

        static async Task DownloadExtractPackage(string dir, string artifact, string repo, string tag)
        {
            var downloaded_artifact = await DownloadArchiveArtifact(artifact, tag, repo);

            var extract_dir = Path.Combine(dir, "unpack");
            if (Path.Exists(extract_dir)) {
                Directory.Delete(extract_dir, true);
                Directory.CreateDirectory(extract_dir);
            }

            Extract(downloaded_artifact, extract_dir);

            var zipExt = ".zip";
            var tarGzExt = ".tar.gz";

            var filename = Path.GetFileName(downloaded_artifact);
            var ext = filename.EndsWith(zipExt) ? zipExt : tarGzExt;
            var extract_dir_nested_name = filename.Substring(0, filename.Length - ext.Length);
            var extract_dir_nested = Path.Combine(extract_dir, extract_dir_nested_name);

            CopyFilesRecursively(extract_dir_nested, dir);

            SetPermissions(dir);

            Directory.Delete(extract_dir, true);

        }

        public static void BuildDirectoryStructure(string gamerhash_dir)
        {
            Directory.CreateDirectory(BinariesDir(gamerhash_dir));
            Directory.CreateDirectory(ProviderDataDir(gamerhash_dir));
            Directory.CreateDirectory(YagnaDataDir(gamerhash_dir));
            Directory.CreateDirectory(ExeUnitsDir(gamerhash_dir));
        }

        // Based on: https://stackoverflow.com/a/3822913
        public static void CopyFilesRecursively(string sourcePath, string targetPath)
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

        public static string TestDir(string name)
        {
            var build_dir = AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? Path.GetTempPath();
            var tests_dir = Path.Combine(build_dir, "..", "..", "..", "tests");
            return Path.GetFullPath(Path.Combine(tests_dir, name));
        }

        public static string BinariesDir(string test_dir)
        {
            return Path.Combine(test_dir, "modules", "golem");
        }

        public static string DataDir(string test_dir)
        {
            return Path.Combine(test_dir, "modules", "golem-data");
        }

        public static string ModulesDir(string test_dir)
        {
            return Path.Combine(test_dir, "modules");
        }

        public static string ProviderDataDir(string test_dir)
        {
            return Path.Combine(DataDir(test_dir), "provider");
        }

        public static string YagnaDataDir(string test_dir)
        {
            return Path.Combine(DataDir(test_dir), "yagna");
        }

        public static string ExeUnitsDir(string test_dir)
        {
            return Path.Combine(test_dir, "modules", "plugins");
        }

        internal static async Task<string> Download(string url)
        {
            var name = Path.GetFileName(url);
            var target_dir = Path.Combine(Path.GetTempPath(), "gamerhash_facade_tests");
            var target_file = Path.Combine(target_dir, name);
            if (Path.Exists(target_file))
            {
                return target_file;
            }
            if (!Directory.Exists(target_dir))
            {
                Directory.CreateDirectory(target_dir);
            }
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
        }

        // Based on: https://stackoverflow.com/a/52200001
        static void ExtractTGZ(string gzArchiveName, string destFolder)
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
                FileSecurity fSecurity = file.GetAccessControl();
                fSecurity.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.ExecuteFile, AccessControlType.Allow));
                file.SetAccessControl(fSecurity);
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


        static async Task<string> DownloadArchiveArtifact(string artifact, string tag, string repository)
        {
            var ext = OperatingSystem.IsWindows() ? "zip" : "tar.gz";
            var system = System();
            var artifact_filename = $"{artifact}-{system}-{tag}.{ext}";
            var url = $"https://github.com/{repository}/releases/download/{tag}/{artifact_filename}";

            Console.WriteLine($"Download archive: {url}");
            return await Download(url);
        }
    }


}
