using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace WipeOut.Services
{
    public class CleanerService
    {
        public async Task<int> CleanLeftoversAsync(List<LeftoverItem> selectedItems)
        {
            int cleanedCount = 0;

            await Task.Run(() =>
            {
                foreach (var item in selectedItems)
                {
                    if (!item.IsSelected) continue;

                    try
                    {
                        if (item.Type == "Folder")
                        {
                            if (Directory.Exists(item.Path))
                            {
                                Directory.Delete(item.Path, true);
                                cleanedCount++;
                            }
                        }
                        else if (item.Type == "File")
                        {
                            if (File.Exists(item.Path))
                            {
                                File.Delete(item.Path);
                                cleanedCount++;
                            }
                        }
                        else if (item.Type == "Registry")
                        {
                            DeleteRegistryKey(item.Path);
                            cleanedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to clean {item.Path}: {ex.Message}");
                    }
                }
            });

            return cleanedCount;
        }

        private void DeleteRegistryKey(string fullPath)
        {
            // Parse HKEY_CURRENT_USER\Software\App to rootKey + subPath
            int firstSlash = fullPath.IndexOf('\\');
            if (firstSlash <= 0) return;

            string rootStr = fullPath.Substring(0, firstSlash);
            string subPath = fullPath.Substring(firstSlash + 1);

            RegistryKey rootKey = rootStr switch
            {
                "HKEY_CURRENT_USER" => Registry.CurrentUser,
                "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
                "HKEY_USERS" => Registry.Users,
                _ => null
            };

            if (rootKey != null)
            {
                rootKey.DeleteSubKeyTree(subPath, false);
            }
        }
    }
}
