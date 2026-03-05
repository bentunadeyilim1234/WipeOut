using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using WipeOut.Helpers;
using WipeOut.Models;

namespace WipeOut.Services
{
    public class Win32AppScanner
    {
        private readonly string cacheDirectory;

        public Win32AppScanner(string cacheDirectory)
        {
            this.cacheDirectory = cacheDirectory;
        }

        public List<InstalledApp> Scan()
        {
            var apps = new List<InstalledApp>();
            
            ScanRegistryKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", apps);
            ScanRegistryKey(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", apps);
            ScanRegistryKey(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", apps);

            return apps;
        }

        private void ScanRegistryKey(RegistryKey rootKey, string path, List<InstalledApp> apps)
        {
            using RegistryKey key = rootKey.OpenSubKey(path);
            if (key == null) return;

            foreach (string subKeyName in key.GetSubKeyNames())
            {
                using RegistryKey subKey = key.OpenSubKey(subKeyName);
                if (subKey == null) continue;

                string displayName = subKey.GetValue("DisplayName") as string;
                string systemComponent = subKey.GetValue("SystemComponent")?.ToString();
                string parentKeyName = subKey.GetValue("ParentKeyName") as string;

                // Skip system components and updates
                if (string.IsNullOrWhiteSpace(displayName) || 
                    systemComponent == "1" || 
                    !string.IsNullOrWhiteSpace(parentKeyName))
                {
                    continue;
                }

                string uninstallString = subKey.GetValue("UninstallString") as string;
                if (string.IsNullOrWhiteSpace(uninstallString))
                {
                    continue;
                }

                string publisher = subKey.GetValue("Publisher") as string ?? "Unknown";
                string displayVersion = subKey.GetValue("DisplayVersion") as string ?? "";
                string installDate = subKey.GetValue("InstallDate") as string ?? "";
                string installLocation = subKey.GetValue("InstallLocation") as string ?? "";
                string displayIcon = subKey.GetValue("DisplayIcon") as string ?? "";
                string quietUninstall = subKey.GetValue("QuietUninstallString") as string ?? "";

                // Get ID based on the registry key name
                string id = subKeyName;

                // Calculate or read size
                long size = 0;
                object estSizeObj = subKey.GetValue("EstimatedSize");
                if (estSizeObj is int estSizeInt)
                {
                    // EstimatedSize is typically in KB
                    size = (long)estSizeInt * 1024;
                }
                else if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
                {
                    size = SizeHelper.GetDirectorySize(installLocation);
                }

                // Extract Icon
                string iconPath = IconExtractor.ExtractAndSaveIcon(displayIcon, installLocation, uninstallString, displayName, id, cacheDirectory);

                // Prevent duplicates based on UninstallString and DisplayName
                if (!apps.Exists(a => a.DisplayName == displayName && a.UninstallString == uninstallString))
                {
                    apps.Add(new InstalledApp
                    {
                        Id = id,
                        DisplayName = displayName,
                        Publisher = publisher,
                        DisplayVersion = displayVersion,
                        InstallDate = installDate,
                        InstallLocation = installLocation,
                        UninstallString = uninstallString,
                        QuietUninstallString = quietUninstall,
                        RegistryKeyPath = $"{rootKey.Name}\\{path}\\{subKeyName}",
                        Type = AppType.Win32,
                        EstimatedSize = size,
                        IconPath = iconPath
                    });
                }
            }
        }
    }
}
