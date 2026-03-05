using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;
using WipeOut.Models;
using WipeOut.ViewModels;

namespace WipeOut
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            this.InitializeComponent();

            // Configure window
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1150, Height = 780 });
            
            // Set minimum size via interop
            SetMinWindowSize(hwnd, 900, 600);

            // Extend title bar into content
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(null);

            ViewModel = new MainViewModel();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            AppsListView.SelectionChanged += AppsListView_SelectionChanged;

            _ = ViewModel.LoadAppsAsync();
        }

        private void SetMinWindowSize(IntPtr hwnd, int minWidthDip, int minHeightDip)
        {
            // We use the SubclassProc approach but for simplicity, just set initial size
            // The WinUI 3 framework handles min size through AppWindow in newer SDK versions
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.SelectedApp))
            {
                UpdateDetailsPanel();
            }
            else if (e.PropertyName == nameof(ViewModel.TotalAppCount) ||
                     e.PropertyName == nameof(ViewModel.Win32AppCount) ||
                     e.PropertyName == nameof(ViewModel.WindowsAppCount))
            {
                UpdateBadges();
            }
            else if (e.PropertyName == nameof(ViewModel.FilteredApps))
            {
                UpdateFilteredCount();
            }
        }

        private void UpdateBadges()
        {
            BadgeWin32.Value = ViewModel.Win32AppCount;
            BadgeUwp.Value = ViewModel.WindowsAppCount;
            UpdateFilteredCount();
        }

        private void UpdateFilteredCount()
        {
            FilteredCountText.Text = $"{ViewModel.FilteredApps.Count} apps";
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                string tag = item.Tag?.ToString() ?? "Win32";

                if (tag == "Refresh" || tag == "About")
                {
                    if (tag == "Refresh")
                    {
                        _ = ViewModel.LoadAppsAsync();
                    }
                    else if (tag == "About")
                    {
                        _ = ShowAboutDialogAsync();
                    }

                    // Reselect previous item
                    switch (ViewModel.CurrentFilter)
                    {
                        case "WindowsApp": NavView.SelectedItem = NavWindowsApps; break;
                        default: NavView.SelectedItem = NavWin32; break;
                    }
                    return;
                }

                ViewModel.CurrentFilter = tag;
                ViewModel.SelectedApp = null;

                switch (tag)
                {
                    case "Win32":
                        PageTitle.Text = "Desktop Apps";
                        break;
                    case "WindowsApp":
                        PageTitle.Text = "Windows Apps";
                        break;
                }

                UpdateFilteredCount();
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                ViewModel.SearchText = sender.Text;
                UpdateFilteredCount();
            }
        }

        private void AppsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDetailsPanel();
        }

        private void UpdateDetailsPanel()
        {
            var app = ViewModel.SelectedApp;
            if (app == null)
            {
                PlaceholderPanel.Visibility = Visibility.Visible;
                DetailContent.Visibility = Visibility.Collapsed;
                return;
            }

            PlaceholderPanel.Visibility = Visibility.Collapsed;
            DetailContent.Visibility = Visibility.Visible;

            // Icon via ImageBrush
            if (!string.IsNullOrEmpty(app.IconPath) && File.Exists(app.IconPath))
            {
                try
                {
                    DetailIconBrush.ImageSource = new BitmapImage(new Uri(app.IconPath));
                }
                catch
                {
                    DetailIconBrush.ImageSource = null;
                }
            }
            else
            {
                DetailIconBrush.ImageSource = null;
            }

            DetailName.Text = app.DisplayName;
            DetailPublisher.Text = app.Publisher;

            // Type badge
            if (app.Type == AppType.Win32)
            {
                DetailTypeBadgeText.Text = "Desktop App";
                DetailTypeBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 120, 212));
            }
            else
            {
                DetailTypeBadgeText.Text = "Windows App";
                DetailTypeBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 16, 137, 62));
            }

            DetailVersion.Text = string.IsNullOrWhiteSpace(app.DisplayVersion) ? "—" : app.DisplayVersion;
            DetailDate.Text = string.IsNullOrWhiteSpace(app.InstallDate) ? "—" : app.InstallDate;
            DetailSize.Text = app.FormattedSize;
        }

        private async void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            var app = ViewModel.SelectedApp;
            if (app == null) return;

            // Step 1: Confirm with advanced options
            var optionsPanel = new StackPanel { Spacing = 16 };
            
            var headerText = new TextBlock
            {
                Text = $"Are you sure you want to uninstall {app.DisplayName}?\nChoose your uninstall options below:",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            optionsPanel.Children.Add(headerText);

            var restorePointCheckBox = new CheckBox
            {
                Content = "Create a System Restore Point before uninstalling",
                IsChecked = true
            };
            optionsPanel.Children.Add(restorePointCheckBox);

            var cleanModeHeader = new TextBlock { Text = "Deep Clean Mode:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
            optionsPanel.Children.Add(cleanModeHeader);

            var safeModeRadio = new RadioButton { Content = "Safe (Only registry and files directly linked to the app)", GroupName = "CleanMode" };
            var moderateModeRadio = new RadioButton { Content = "Moderate (Includes common publisher folders and AppData)", GroupName = "CleanMode", IsChecked = true };
            var aggressiveModeRadio = new RadioButton { Content = "Aggressive (Deepest scan, may include shared components)", GroupName = "CleanMode" };
            
            optionsPanel.Children.Add(safeModeRadio);
            optionsPanel.Children.Add(moderateModeRadio);
            optionsPanel.Children.Add(aggressiveModeRadio);

            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Uninstall & Deep Clean Options",
                Content = optionsPanel,
                PrimaryButtonText = "Continue",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            // System Restore
            ViewModel.IsLoading = true;
            if (restorePointCheckBox.IsChecked == true)
            {
                ViewModel.LoadingMessage = "Creating System Restore Point...";
                var restoreService = new WipeOut.Services.SystemRestoreService();
                await restoreService.CreateRestorePointAsync($"WipeOut removed {app.DisplayName}");
            }

            // Step 2: Uninstall
            ViewModel.LoadingMessage = $"Uninstalling {app.DisplayName}...";

            var uninstallerService = new WipeOut.Services.UninstallerExecutionService();
            await uninstallerService.UninstallAppAsync(app);
            await Task.Delay(1000); // Give filesystem a moment to sync

            // Step 3: Deep scan
            ViewModel.LoadingMessage = $"Scanning for leftovers...";
            var scanner = new WipeOut.Services.DeepCleanScanner();
            
            // Map the selected radio to an enum or flag (For now just pass app, DeepCleanScanner can be updated to handle mode later)
            // For example: 0 = Safe, 1 = Moderate, 2 = Aggressive
            int cleanMode = safeModeRadio.IsChecked == true ? 0 : (moderateModeRadio.IsChecked == true ? 1 : 2);
            var leftovers = await scanner.ScanForLeftoversAsync(app); // Currently doesn't use cleanMode, we will pass it in later.

            ViewModel.IsLoading = false;
            ViewModel.LoadingMessage = string.Empty;

            if (leftovers.Count == 0)
            {
                await ShowInfoDialog("Clean Completed", "No leftover files or registry keys were found.");
                _ = ViewModel.LoadAppsAsync();
                return;
            }

            // Step 4: Show leftovers
            StackPanel sp = new StackPanel { Spacing = 10 };
            sp.Children.Add(new TextBlock
            {
                Text = $"Found {leftovers.Count} leftover items. Uncheck any you want to keep.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8
            });

            ListView lv = new ListView
            {
                ItemsSource = leftovers,
                SelectionMode = ListViewSelectionMode.None,
                MaxHeight = 400
            };

            string templateStr = @"
            <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                <StackPanel Orientation='Horizontal' Spacing='10' Padding='4'>
                    <CheckBox IsChecked='{Binding IsSelected, Mode=TwoWay}' Content='{Binding Type}' FontWeight='SemiBold' MinWidth='80'/>
                    <TextBlock Text='{Binding Path}' VerticalAlignment='Center' TextWrapping='WrapWholeWords' Opacity='0.7' FontSize='12'/>
                </StackPanel>
            </DataTemplate>";
            lv.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(templateStr);
            sp.Children.Add(lv);

            ContentDialog leftoversDialog = new ContentDialog
            {
                Title = "Select Leftovers to Delete",
                Content = sp,
                PrimaryButtonText = "Delete Selected",
                CloseButtonText = "Skip",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var cleanResult = await leftoversDialog.ShowAsync();
            if (cleanResult == ContentDialogResult.Primary)
            {
                ViewModel.IsLoading = true;
                ViewModel.LoadingMessage = "Cleaning leftovers...";

                var cleaner = new WipeOut.Services.CleanerService();
                int cleaned = await cleaner.CleanLeftoversAsync(leftovers);

                ViewModel.IsLoading = false;
                await ShowInfoDialog("Deep Clean Finished", $"Successfully removed {cleaned} leftover items.");
            }

            _ = ViewModel.LoadAppsAsync();
        }

        private async Task ShowInfoDialog(string title, string message)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async Task ShowAboutDialogAsync()
        {
            var dialog = new ContentDialog
            {
                Title = "About WipeOut",
                Content = new TextBlock
                {
                    Text = "WipeOut is an advanced application deep-cleaner and uninstaller.\nVersion 1.0\n\nFeatures:\n- Standard uninstallation integration\n- Deep cleaning of Registry and Files\n- Modern Windows 11 Design",
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = "Close",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
