# WipeOut

WipeOut is an advanced application uninstaller and deep-cleaner for Windows, designed with a modern Windows 11 fluent aesthetic. It helps you thoroughly remove both Desktop (Win32) and Universal Windows Platform (UWP) apps while meticulously sweeping away leftover files and registry entries to keep your system fast and clean.

## Features

- **Modern WinUI 3 Design**: Built using the latest Windows app development frameworks for a flawless, native Windows 11 look and feel featuring Mica backdrops and intuitive interfaces.
- **Deep Scanning Engine**: Like Revo Uninstaller, WipeOut tracks down elusive leftover files, folders, and registry keys left behind by standard uninstallation routines.
- **Aggressive Icon Extraction**: Extracts high-quality icons directly from application executables and registry paths to ensure a visually beautiful app list.
- **System Restore Integration**: Automatically creates Windows System Restore points prior to uninstallation so you always have a fallback.
- **Granular Control**: Review exactly which leftover files or registry entries will be deleted before they are removed.
- **Responsive Navigation**: Easily switch between sorting, searching, and filtering all Installed Desktop-class and Windows Store applications.

## Quick Start

### Prerequisites
- Windows 10 (Version 19041 or higher) or Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) 

### Running Locally
1. Clone the repository: `git clone https://github.com/yourusername/WipeOut.git`
2. Navigate to the project directory: `cd WipeOut`
3. Run the application (no build required or package deployment necessary):
   ```bash
   dotnet run
   ```

## Creating a Release (Building)
WipeOut leverages a GitHub Actions CI pipeline to build out self-contained releases, but you can trigger a standalone build locally:
```bash
dotnet build -c Release
```

## Contributing
We welcome contributions to make WipeOut the fastest, smartest, and cleanest uninstaller on Windows!

1. **Fork the repostiory**.
2. **Create a Feature Branch** (`git checkout -b feature/FastRegistryScanner`).
3. **Commit your changes** (`git commit -m 'Add blistering fast registry scanner'`).
4. **Push to the branch** (`git push origin feature/FastRegistryScanner`).
5. **Open a Pull Request** against the `main` branch.

Please review our [Pull Request Template](.github/PULL_REQUEST_TEMPLATE.md) to ensure all tests pass and your code is documented.

## Reporting Bugs / Asking for Features
Encountered an issue or want to suggest a cool new deep-clean feature?
- [Submit a Bug Report](.github/ISSUE_TEMPLATE/bug_report.md)
- [Request a Feature](.github/ISSUE_TEMPLATE/feature_request.md)

## License
Distributed under the MIT License. See `LICENSE` for more information.
