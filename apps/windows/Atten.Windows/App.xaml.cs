using Microsoft.UI.Xaml;

namespace Atten.Windows;

public partial class App : Application
{
    private Window? window;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            LogFatalCrash(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "Unknown fatal error"));
        };

        UnhandledException += (s, e) =>
        {
            LogFatalCrash(e.Exception);
            e.Handled = true;
        };

        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            if (Environment.GetCommandLineArgs().Contains("--validate-install", StringComparer.OrdinalIgnoreCase))
            {
                _ = ValidateInstallationAsync();
                return;
            }

            window = new MainWindow();
            window.Activate();
        }
        catch (Exception ex)
        {
            LogFatalCrash(ex);
            throw;
        }
    }

    private static void LogFatalCrash(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Atten");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, "crash.log");
            File.AppendAllText(logFile, $"[{DateTime.UtcNow:O}] Fatal error:\n{ex}\n\n");
        }
        catch
        {
            // Ignore logging failures
        }
    }

    // The release build invokes this mode from the staged publish directory.
    // It proves that WinUI can initialize without a separately installed
    // Windows App SDK and that the bundled backend, model, voice catalog,
    // and UI XAML components are all reachable before an installer is published.
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
            var testWindow = new MainWindow();
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
