using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;
using Golem.Tablet.Mvvm;
using Golem.Tablet.Services;

namespace Golem.Tablet.ViewModels;

public sealed class AboutViewModel : ObservableObject
{
    public const string GitHubProfile = "https://github.com/M-GOLEM";

    private readonly IDialogService _dialogs;

    public AboutViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        OpenProfileCommand = new RelayCommand(OpenProfile);
    }

    public string AppName => "GOLEM TABLET";

    public string Developer => "M-GOLEM";

    public string Version
    {
        get
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "1.0.0" : version.ToString(3);
        }
    }

    public string ProfileUrl => GitHubProfile;

    public string Summary =>
        "Tablet device management and diagnostics for Windows: device inventory, ADB tooling, " +
        "AT-command diagnostics, driver inspection, and firmware-package validation.";

    public string PrivacyStatement =>
        "This build is local-only. It has no update service, no telemetry, and makes no network " +
        "connections of its own. Firmware is validated on disk and devices are accessed through " +
        "standard Windows APIs and user-supplied tools.";

    public string ThirdPartyNote =>
        "Built on .NET 8 with System.Management and System.IO.Ports (MIT). " +
        "See THIRD-PARTY-NOTICES.md in the repository.";

    public ICommand OpenProfileCommand { get; }

    private void OpenProfile()
    {
        try
        {
            Process.Start(new ProcessStartInfo(GitHubProfile) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _dialogs.ShowInfo("Open profile", "Could not open the browser: " + ex.Message);
        }
    }
}
