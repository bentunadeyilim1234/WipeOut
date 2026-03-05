using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WipeOut.Models;
using WipeOut.Services;

namespace WipeOut.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly Win32AppScanner _win32Scanner;
        private readonly WindowsAppScanner _windowsAppScanner;
        private readonly AppCacheService _cacheService;
        private readonly UninstallerExecutionService _uninstallerService;
        private readonly DeepCleanScanner _deepCleanScanner;
        private readonly CleanerService _cleanerService;

        private List<InstalledApp> _allApps = new();

        [ObservableProperty]
        private ObservableCollection<InstalledApp> _filteredApps = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _loadingMessage = string.Empty;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private InstalledApp? _selectedApp;

        [ObservableProperty]
        private string _currentFilter = "Win32"; // "Win32", "WindowsApp"

        [ObservableProperty]
        private string _sortOption = "Name"; // "Name", "Size", "Date"

        [ObservableProperty]
        private bool _sortDescending = false;

        [ObservableProperty]
        private int _totalAppCount;

        [ObservableProperty]
        private int _win32AppCount;

        [ObservableProperty]
        private int _windowsAppCount;

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        partial void OnCurrentFilterChanged(string value)
        {
            ApplyFilter();
        }

        partial void OnSortOptionChanged(string value)
        {
            ApplyFilter();
        }

        partial void OnSortDescendingChanged(bool value)
        {
            ApplyFilter();
        }

        public MainViewModel()
        {
            _cacheService = new AppCacheService();
            string cacheDir = _cacheService.CacheDirectory;
            _win32Scanner = new Win32AppScanner(cacheDir);
            _windowsAppScanner = new WindowsAppScanner(cacheDir);
            _uninstallerService = new UninstallerExecutionService();
            _deepCleanScanner = new DeepCleanScanner();
            _cleanerService = new CleanerService();
        }

        [RelayCommand]
        public async Task LoadAppsAsync()
        {
            IsLoading = true;
            LoadingMessage = "Loading apps from cache...";

            try
            {
                // Try to load from cache first
                var cachedApps = await _cacheService.LoadCacheAsync();
                if (cachedApps.Any())
                {
                    _allApps = cachedApps.ToList();
                    UpdateCounts();
                    ApplyFilter();
                }

                // Run background scan to update cache
                LoadingMessage = "Scanning installed applications...";
                
                var win32Task = Task.Run(() => _win32Scanner.Scan());
                var uwpTask = _windowsAppScanner.ScanAsync();

                await Task.WhenAll(win32Task, uwpTask);

                var newApps = new List<InstalledApp>();
                newApps.AddRange(win32Task.Result);
                newApps.AddRange(uwpTask.Result);

                _allApps = newApps.ToList();

                // Save new cache
                await _cacheService.SaveCacheAsync(_allApps);

                UpdateCounts();
                ApplyFilter();
            }
            finally
            {
                IsLoading = false;
                LoadingMessage = string.Empty;
            }
        }

        private void UpdateCounts()
        {
            TotalAppCount = _allApps.Count;
            Win32AppCount = _allApps.Count(a => a.Type == AppType.Win32);
            WindowsAppCount = _allApps.Count(a => a.Type == AppType.WindowsApp);
        }

        private void ApplyFilter()
        {
            FilteredApps.Clear();

            IEnumerable<InstalledApp> query = _allApps;

            // Filter by type
            if (CurrentFilter == "Win32")
                query = query.Where(a => a.Type == AppType.Win32);
            else if (CurrentFilter == "WindowsApp")
                query = query.Where(a => a.Type == AppType.WindowsApp);

            // Filter by search text
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(a =>
                    a.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    a.Publisher.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            // Apply Sorting
            switch (SortOption)
            {
                case "Size":
                    query = SortDescending ? query.OrderByDescending(a => a.EstimatedSize) : query.OrderBy(a => a.EstimatedSize);
                    break;
                case "Date":
                    query = SortDescending ? query.OrderByDescending(a => a.InstallDate) : query.OrderBy(a => a.InstallDate);
                    break;
                case "Name":
                default:
                    query = SortDescending ? query.OrderByDescending(a => a.DisplayName) : query.OrderBy(a => a.DisplayName);
                    break;
            }

            foreach (var app in query)
            {
                FilteredApps.Add(app);
            }
        }
    }
}
