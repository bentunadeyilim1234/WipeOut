using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using Windows.Storage.Streams;
using WipeOut.Models;

namespace WipeOut.Services
{
    public class WindowsAppScanner
    {
        private readonly string cacheDirectory;

        public WindowsAppScanner(string cacheDirectory)
        {
            this.cacheDirectory = cacheDirectory;
        }

        public async Task<List<InstalledApp>> ScanAsync()
        {
            var apps = new List<InstalledApp>();
            
            try
            {
                PackageManager packageManager = new PackageManager();
                var packages = packageManager.FindPackagesForUser(string.Empty);

                foreach (var package in packages)
                {
                    // Filter out frameworks and system packages
                    if (package.IsFramework || package.IsResourcePackage || package.IsStub)
                        continue;

                    // Some packages don't have a display name or throw access denied
                    try
                    {
                        var appListEntries = await package.GetAppListEntriesAsync();
                        if (appListEntries == null || appListEntries.Count == 0) continue;

                        var primaryApp = appListEntries[0];
                        string displayName = primaryApp.DisplayInfo.DisplayName;

                        if (string.IsNullOrWhiteSpace(displayName))
                            continue;

                        // App extraction might fail or throw, so we wrap it
                        string iconPath = await ExtractLogoAsync(primaryApp, package.Id.FullName);
                        long size = 0;

                        try
                        {
                            if (package.InstalledLocation != null)
                            {
                                size = WipeOut.Helpers.SizeHelper.GetDirectorySize(package.InstalledLocation.Path);
                            }
                        }
                        catch
                        {
                            // access denied to InstalledLocation often happens for UWP apps
                        }

                        apps.Add(new InstalledApp
                        {
                            Id = package.Id.FullName,
                            DisplayName = displayName,
                            Publisher = package.PublisherDisplayName,
                            DisplayVersion = $"{package.Id.Version.Major}.{package.Id.Version.Minor}.{package.Id.Version.Build}",
                            InstallLocation = package.InstalledLocation?.Path ?? "",
                            UninstallString = "UWP", // Special marker for UI to know how to uninstall
                            PackageFullName = package.Id.FullName,
                            Type = AppType.WindowsApp,
                            EstimatedSize = size,
                            IconPath = iconPath
                        });
                    }
                    catch
                    {
                        // Skip packages that error out
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to scan UWP apps: {ex.Message}");
            }

            return apps;
        }

        private async Task<string> ExtractLogoAsync(Windows.ApplicationModel.Core.AppListEntry appListEntry, string fullPackageName)
        {
            try
            {
                // Sanitize the filename
                string safeName = fullPackageName;
                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(c, '_');
                }

                string savePath = Path.Combine(cacheDirectory, $"{safeName}_logo.png");

                // If we already cached this icon, skip re-extraction
                if (File.Exists(savePath))
                {
                    var fi = new FileInfo(savePath);
                    if (fi.Length > 0) return savePath;
                }

                // Request the square logo at a large size so we always get good quality
                var logoStreamRef = appListEntry.DisplayInfo.GetLogo(new Windows.Foundation.Size(256, 256));
                if (logoStreamRef == null) return string.Empty;

                var streamWithContent = await logoStreamRef.OpenReadAsync();
                if (streamWithContent == null) return string.Empty;

                using (var winrtStream = streamWithContent.AsStreamForRead())
                {
                    // Read entire stream into a MemoryStream first
                    using (var ms = new MemoryStream())
                    {
                        await winrtStream.CopyToAsync(ms);
                        ms.Position = 0;

                        // Use System.Drawing to normalize the image into a 128x128 square
                        using (var original = System.Drawing.Image.FromStream(ms))
                        {
                            int targetSize = 128;
                            using (var bitmap = new System.Drawing.Bitmap(targetSize, targetSize))
                            {
                                bitmap.SetResolution(96, 96);
                                using (var g = System.Drawing.Graphics.FromImage(bitmap))
                                {
                                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                    g.Clear(System.Drawing.Color.Transparent);

                                    // Calculate scaling to fill the square uniformly
                                    float scale = Math.Max(
                                        (float)targetSize / original.Width,
                                        (float)targetSize / original.Height);

                                    int scaledW = (int)(original.Width * scale);
                                    int scaledH = (int)(original.Height * scale);
                                    int offsetX = (targetSize - scaledW) / 2;
                                    int offsetY = (targetSize - scaledH) / 2;

                                    g.DrawImage(original, offsetX, offsetY, scaledW, scaledH);
                                }

                                bitmap.Save(savePath, System.Drawing.Imaging.ImageFormat.Png);
                            }
                        }
                    }
                }

                return savePath;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
