using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WipeOut.Models;

namespace WipeOut.Services
{
    public class LeftoverItem
    {
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "File", "Folder", "Registry"
        public bool IsSelected { get; set; } = true;

        public LeftoverItem(string path, string type)
        {
            Path = path;
            Type = type;
        }
    }

    public class DeepCleanScanner
    {
        public async Task<List<LeftoverItem>> ScanForLeftoversAsync(InstalledApp app)
        {
            var leftovers = new List<LeftoverItem>();

            await Task.Run(() =>
            {
                string searchKeyword = ExtractCoreSearchName(app.DisplayName ?? "");
                string publisherKeyword = ExtractCoreSearchName(app.Publisher ?? "");

                if (string.IsNullOrWhiteSpace(searchKeyword))
                    return;

                // 1. Scan File System
                ScanFileSystem(searchKeyword, publisherKeyword, leftovers);

                // 2. Scan Registry
                ScanRegistry(Registry.CurrentUser, @"Software", searchKeyword, publisherKeyword, leftovers);
                ScanRegistry(Registry.LocalMachine, @"SOFTWARE", searchKeyword, publisherKeyword, leftovers);
                ScanRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node", searchKeyword, publisherKeyword, leftovers);

                // 3. Include original InstallLocation if it still exists
                if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation))
                {
                    leftovers.Add(new LeftoverItem(app.InstallLocation, "Folder"));
                }
            });

            return leftovers;
        }

        private string ExtractCoreSearchName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            string cleaned = name;
            // Remove common suffixes that clutter search
            string[] suffixes = { " Inc.", " Corp.", " LLC", " (x86)", " (x64)", " Setup" };
            foreach (var s in suffixes)
            {
                cleaned = cleaned.Replace(s, "", StringComparison.OrdinalIgnoreCase);
            }

            // Optional: trim version numbers from the name if you have them e.g. "App v2.1"
            cleaned = Regex.Replace(cleaned, @"\s+v?\d+(\.\d+)*.*$", "");

            return cleaned.Trim();
        }

        private void ScanFileSystem(string searchKeyword, string publisher, List<LeftoverItem> leftovers)
        {
            string[] basePaths = {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) // ProgramData
            };

            foreach (var path in basePaths)
            {
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) continue;

                // We want to find folders that EXACTLY match or start with the keyword
                try
                {
                    var dirs = Directory.GetDirectories(path);
                    foreach (var dir in dirs)
                    {
                        string dirName = Path.GetFileName(dir);
                        
                        // Strict enough match to avoid deleting system folders 
                        bool isMatch = dirName.Equals(searchKeyword, StringComparison.OrdinalIgnoreCase) ||
                                       (!string.IsNullOrWhiteSpace(publisher) && dirName.Equals(publisher, StringComparison.OrdinalIgnoreCase));

                        if (isMatch)
                        {
                            leftovers.Add(new LeftoverItem(dir, "Folder"));
                        }
                        else if (!string.IsNullOrWhiteSpace(publisher) && dirName.Equals(publisher, StringComparison.OrdinalIgnoreCase))
                        {
                            // if there's a publisher folder, check inside for the app folder
                            try
                            {
                                var subDirs = Directory.GetDirectories(dir);
                                foreach (var subDir in subDirs)
                                {
                                    string subDirName = Path.GetFileName(subDir);
                                    if (subDirName.IndexOf(searchKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        leftovers.Add(new LeftoverItem(subDir, "Folder"));
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (UnauthorizedAccessException) { }
            }
        }

        private void ScanRegistry(RegistryKey rootKey, string path, string searchKeyword, string publisher, List<LeftoverItem> leftovers)
        {
            try
            {
                using RegistryKey key = rootKey.OpenSubKey(path);
                if (key == null) return;

                foreach (string subKeyName in key.GetSubKeyNames())
                {
                    bool isMatch = subKeyName.Equals(searchKeyword, StringComparison.OrdinalIgnoreCase) ||
                                   (!string.IsNullOrWhiteSpace(publisher) && subKeyName.Equals(publisher, StringComparison.OrdinalIgnoreCase));

                    if (isMatch)
                    {
                        leftovers.Add(new LeftoverItem($@"{rootKey.Name}\{path}\{subKeyName}", "Registry"));
                    }
                    else if (!string.IsNullOrWhiteSpace(publisher) && subKeyName.Equals(publisher, StringComparison.OrdinalIgnoreCase))
                    {
                         // also check inside publisher key
                         try
                         {
                             using RegistryKey pubKey = key.OpenSubKey(subKeyName);
                             if (pubKey != null)
                             {
                                 foreach(string appKey in pubKey.GetSubKeyNames())
                                 {
                                     if (appKey.IndexOf(searchKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                                     {
                                         leftovers.Add(new LeftoverItem($@"{rootKey.Name}\{path}\{subKeyName}\{appKey}", "Registry"));
                                     }
                                 }
                             }
                         }
                         catch { }
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
