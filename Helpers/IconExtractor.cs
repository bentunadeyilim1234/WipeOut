using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace WipeOut.Helpers
{
    public static class IconExtractor
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>
        /// Aggressively extracts an icon from registry, uninstall string or installation directory.
        /// </summary>
        public static string ExtractAndSaveIcon(string displayIcon, string installLocation, string uninstallString, string displayName, string appId, string cacheDirectory)
        {
            if (string.IsNullOrWhiteSpace(cacheDirectory))
                return string.Empty;

            string savePath = Path.Combine(cacheDirectory, $"{SanitizeFileName(appId)}.png");
            if (File.Exists(savePath))
            {
                var fi = new FileInfo(savePath);
                if (fi.Length > 0) return savePath;
            }

            // Attempt 1: DisplayIcon registry value
            if (TryExtract(displayIcon, savePath)) return savePath;

            // Attempt 2: Extract from UninstallString (often contains unins000.exe or similar with icon)
            string uninstallExe = ExtractExePath(uninstallString);
            if (TryExtract(uninstallExe, savePath)) return savePath;

            // Attempt 3: Search Install Location for .exe files
            if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
            {
                try
                {
                    var exeFiles = Directory.GetFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly);
                    if (exeFiles.Length > 0)
                    {
                        // Try exact name match first
                        var exactMatch = exeFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(displayName, StringComparison.OrdinalIgnoreCase));
                        if (exactMatch != null && TryExtract(exactMatch, savePath)) return savePath;

                        // Try first largest executable (usually the main app)
                        var largestExe = exeFiles.OrderByDescending(f => new FileInfo(f).Length).First();
                        if (TryExtract(largestExe, savePath)) return savePath;
                        
                        // Final fallback: try every single exe in the folder
                        foreach (var exe in exeFiles)
                        {
                            if (TryExtract(exe, savePath)) return savePath;
                        }
                    }
                }
                catch
                {
                    // Ignore access errors
                }
            }

            return string.Empty;
        }

        private static bool TryExtract(string iconPathAttribute, string savePath)
        {
            if (string.IsNullOrWhiteSpace(iconPathAttribute)) return false;

            try
            {
                string filePath = iconPathAttribute.Trim('"');
                int iconIndex = 0;

                int commaIndex = filePath.LastIndexOf(',');
                if (commaIndex > 0)
                {
                    if (int.TryParse(filePath.Substring(commaIndex + 1), out int parsedIndex))
                    {
                        iconIndex = parsedIndex;
                    }
                    filePath = filePath.Substring(0, commaIndex);
                }

                if (!File.Exists(filePath)) return false;

                IntPtr[] phiconLarge = new IntPtr[1];
                IntPtr[] phiconSmall = new IntPtr[1];

                ExtractIconEx(filePath, iconIndex, phiconLarge, phiconSmall, 1);
                IntPtr hIcon = phiconLarge[0] != IntPtr.Zero ? phiconLarge[0] : phiconSmall[0];

                if (hIcon != IntPtr.Zero)
                {
                    using (Icon icon = Icon.FromHandle(hIcon))
                    using (Bitmap bitmap = icon.ToBitmap())
                    {
                        bitmap.Save(savePath, System.Drawing.Imaging.ImageFormat.Png);
                    }

                    DestroyIcon(phiconLarge[0]);
                    DestroyIcon(phiconSmall[0]);
                    return true;
                }
                else
                {
                    // Aggressive fallback: Use ExtractAssociatedIcon to get the default binded icon
                    try
                    {
                        using (Icon icon = Icon.ExtractAssociatedIcon(filePath))
                        {
                            if (icon != null)
                            {
                                using (Bitmap bitmap = icon.ToBitmap())
                                {
                                    bitmap.Save(savePath, System.Drawing.Imaging.ImageFormat.Png);
                                }
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore extraction failures
                    }
                }
            }
            catch
            {
                // Ignore extraction failures
            }

            return false;
        }

        private static string ExtractExePath(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return string.Empty;

            if (command.StartsWith("\""))
            {
                int endQuote = command.IndexOf("\"", 1);
                if (endQuote > 0)
                {
                    return command.Substring(1, endQuote - 1);
                }
            }
            else
            {
                int exeIndex = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (exeIndex > 0)
                {
                    return command.Substring(0, exeIndex + 4);
                }
            }
            return string.Empty;
        }

        private static string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }
    }
}
