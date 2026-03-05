using System;
using System.IO;

namespace WipeOut.Helpers
{
    public static class SizeHelper
    {
        /// <summary>
        /// Calculates the total size of a directory including subdirectories.
        /// </summary>
        /// <param name="folderPath">The folder to calculate.</param>
        /// <returns>Total size in bytes, or 0 if inaccessible or error.</returns>
        public static long GetDirectorySize(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return 0;
            }

            try
            {
                long size = 0;
                var dirInfo = new DirectoryInfo(folderPath);

                // Add file sizes.
                var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    size += file.Length;
                }

                return size;
            }
            catch (UnauthorizedAccessException)
            {
                // We might not have access to some subdirectories.
                // We could do a more fine-grained, recursive search ignoring exceptions, 
                // but for speed and simplicity, we just return what we can handle or 0.
                return CalculateSizeSafely(new DirectoryInfo(folderPath));
            }
            catch
            {
                return 0;
            }
        }

        private static long CalculateSizeSafely(DirectoryInfo dir)
        {
            long size = 0;
            try
            {
                var files = dir.GetFiles();
                foreach (var file in files)
                {
                    size += file.Length;
                }

                var subDirs = dir.GetDirectories();
                foreach (var sub in subDirs)
                {
                    size += CalculateSizeSafely(sub);
                }
            }
            catch
            {
                // Ignore access denied exceptions
            }

            return size;
        }
    }
}
