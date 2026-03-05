using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using WipeOut.Models;

namespace WipeOut.Services
{
    public class UninstallerExecutionService
    {
        public async Task<bool> UninstallAppAsync(InstalledApp app)
        {
            if (app.Type == AppType.WindowsApp)
            {
                return await UninstallWindowsAppAsync(app.PackageFullName);
            }
            else
            {
                return await UninstallWin32AppAsync(app.UninstallString);
            }
        }

        private async Task<bool> UninstallWindowsAppAsync(string packageFullName)
        {
            try
            {
                PackageManager packageManager = new PackageManager();
                var result = await packageManager.RemovePackageAsync(packageFullName);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error uninstalling UWP app: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> UninstallWin32AppAsync(string uninstallString)
        {
            if (string.IsNullOrWhiteSpace(uninstallString))
                return false;

            try
            {
                string command = uninstallString;
                string arguments = string.Empty;

                if (command.StartsWith("MsiExec.exe", StringComparison.OrdinalIgnoreCase) || 
                    command.StartsWith("msiexec", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(command, @"msiexec\.exe\s+(.*)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        arguments = match.Groups[1].Value;
                        command = "msiexec.exe";
                    }
                }
                else
                {
                    if (command.StartsWith("\""))
                    {
                        int endQuote = command.IndexOf("\"", 1);
                        if (endQuote > 0)
                        {
                            arguments = command.Substring(endQuote + 1).Trim();
                            command = command.Substring(1, endQuote - 1);
                        }
                    }
                    else
                    {
                        int exeIndex = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                        if (exeIndex > 0)
                        {
                            arguments = command.Substring(exeIndex + 4).Trim();
                            command = command.Substring(0, exeIndex + 4);
                        }
                    }
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                using Process process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();

                    // Some uninstallers (like InnoSetup) spawn a child process in %TEMP% and exit immediately.
                    // To approximate waiting for the actual uninstaller, we wait a few seconds and check for common uninstaller process names.
                    string processName = Path.GetFileNameWithoutExtension(command);
                    if (processName.StartsWith("unins", StringComparison.OrdinalIgnoreCase) || 
                        processName.StartsWith("uninstall", StringComparison.OrdinalIgnoreCase) ||
                        processName.StartsWith("AU", StringComparison.OrdinalIgnoreCase))
                    {
                        await WaitForProcessByNameAsync(processName);
                    }

                    return process.ExitCode == 0 || process.ExitCode == 3010;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error running uninstaller: {ex.Message}");
                return false;
            }
        }

        private async Task WaitForProcessByNameAsync(string processName)
        {
            // Wait up to 5 minutes for processes with this name to exit
            int maxRetries = 300; 
            while (maxRetries > 0)
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                {
                    // No such process running anymore
                    break;
                }
                
                await Task.Delay(1000);
                maxRetries--;
            }
        }
    }
}
