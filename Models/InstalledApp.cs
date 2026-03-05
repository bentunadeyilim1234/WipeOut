using System;

namespace WipeOut.Models
{
    public enum AppType
    {
        Win32,
        WindowsApp
    }

    public class InstalledApp
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string DisplayVersion { get; set; } = string.Empty;
        
        /// <summary>
        /// Raw install date if available.
        /// </summary>
        public string InstallDate { get; set; } = string.Empty;

        /// <summary>
        /// Local path on disk where the app icon is extracted/cached.
        /// </summary>
        public string IconPath { get; set; } = string.Empty;

        public AppType Type { get; set; }

        // Win32 specifics
        public string InstallLocation { get; set; } = string.Empty;
        public string UninstallString { get; set; } = string.Empty;
        public string QuietUninstallString { get; set; } = string.Empty;
        public string RegistryKeyPath { get; set; } = string.Empty;

        // Windows App specifics
        public string PackageFullName { get; set; } = string.Empty;

        // Size prediction
        /// <summary>
        /// Estimated size in bytes.
        /// </summary>
        public long EstimatedSize { get; set; }

        public string FormattedSize
        {
            get
            {
                if (EstimatedSize <= 0) return "Unknown size";
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                int order = 0;
                double len = EstimatedSize;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return string.Format("{0:0.##} {1}", len, sizes[order]);
            }
        }
    }
}
