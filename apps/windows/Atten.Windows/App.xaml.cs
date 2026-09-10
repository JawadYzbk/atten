using Microsoft.UI.Xaml;

namespace Atten.Windows;

public partial class App : Application
{
    private Window? window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (Environment.GetCommandLineArgs().Contains("--validate-install", StringComparer.OrdinalIgnoreCase))
        {
            _ = ValidateInstallationAsync();
            return;
        }

        window = new MainWindow();
        window.Activate();
    }

    // The release build invokes this mode from the staged publish directory.
    // It proves that WinUI can initialize without a separately installed
    // Windows App SDK and that the bundled backend, model, and voice catalog
    // are all reachable before an installer is published.
    private static async Task ValidateInstallationAsync()
    {
        var errorPath = Path.Combine(AppContext.BaseDirectory, "install-validation-error.txt");
        try
        {
            if (File.Exists(errorPath))
            {
                File.Delete(errorPath);
            }

            _ = VoiceCatalog.All.Count;
            var info = await new BackendClient().GetInfoAsync(DeviceMode.cpu, CancellationToken.None);
            if (!info.ModelRootValid)
            {
                throw new InvalidOperationException("The bundled Kokoro model failed validation.");
            }

            Environment.Exit(0);
        }
        catch (Exception error)
        {
            try { File.WriteAllText(errorPath, error.ToString()); } catch { }
            Environment.Exit(1);
        }
    }
}
