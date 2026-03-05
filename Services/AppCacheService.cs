using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WipeOut.Models;

namespace WipeOut.Services
{
    public class AppCacheService
    {
        private readonly string cacheDirectory;
        private readonly string cacheFilePath;

        public AppCacheService()
        {
            var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            cacheDirectory = Path.Combine(appDataFolder, "WipeOut", "Cache");
            
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            cacheFilePath = Path.Combine(cacheDirectory, "installed_apps.json");
        }

        public string CacheDirectory => cacheDirectory;

        public async Task SaveCacheAsync(IEnumerable<InstalledApp> apps)
        {
            try
            {
                var json = JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(cacheFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving cache: {ex.Message}");
            }
        }

        public async Task<List<InstalledApp>> LoadCacheAsync()
        {
            if (!File.Exists(cacheFilePath))
            {
                return new List<InstalledApp>();
            }

            try
            {
                var json = await File.ReadAllTextAsync(cacheFilePath);
                var apps = JsonSerializer.Deserialize<List<InstalledApp>>(json);
                return apps ?? new List<InstalledApp>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading cache: {ex.Message}");
                return new List<InstalledApp>();
            }
        }

        public void ClearCache()
        {
            if (File.Exists(cacheFilePath))
            {
                File.Delete(cacheFilePath);
            }
        }
    }
}
