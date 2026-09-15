using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace Atten.Windows;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly StorageService storage = new();
    private readonly BackendClient backend = new();
    private CancellationTokenSource? generationCts;
    private string draftTitle = "Untitled narration";
    private string draftText = "";
    private string selectedVoiceID = "af_heart";
    private string voiceSearchText = "";
    private double speed = 1.0;
    private AudioFormat format = AudioFormat.mp3;
    private DeviceMode deviceMode = DeviceMode.auto;
    private string outputDirectory = "";
    private string status = "";
    private string? currentAudioPath;
    private bool isGenerating;
    private BackendInfo? backendInfo;
    private bool isXttsInstalled;
    private bool isDownloadingModel;
    private bool isDownloadPaused;
    private int downloadProgress;
    private string downloadStatus = "";
    private string downloadSpeed = "";
    private string downloadEta = "";
    private string downloadSizeText = "";
    private CancellationTokenSource? downloadCts;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProjectRecord> Projects { get; } = [];
    public IReadOnlyList<Voice> Voices => VoiceCatalog.All;
    public ObservableCollection<Voice> FilteredVoices { get; } = [];
    public IReadOnlyList<AudioFormat> Formats { get; } = Enum.GetValues<AudioFormat>();
    public IReadOnlyList<DeviceMode> DeviceModes { get; } = Enum.GetValues<DeviceMode>();

    public string VoiceSearchText
    {
        get => voiceSearchText;
        set
        {
            if (Set(ref voiceSearchText, value))
            {
                UpdateFilteredVoices();
            }
        }
    }

    public bool IsXttsInstalled
    {
        get => isXttsInstalled;
        set => Set(ref isXttsInstalled, value);
    }

    public bool IsDownloadingModel
    {
        get => isDownloadingModel;
        set => Set(ref isDownloadingModel, value);
    }

    public bool IsDownloadPaused
    {
        get => isDownloadPaused;
        set => Set(ref isDownloadPaused, value);
    }

    public int DownloadProgress
    {
        get => downloadProgress;
        set => Set(ref downloadProgress, value);
    }

    public string DownloadStatus
    {
        get => downloadStatus;
        set => Set(ref downloadStatus, value);
    }

    public string DownloadSpeed
    {
        get => downloadSpeed;
        set => Set(ref downloadSpeed, value);
    }

    public string DownloadEta
    {
        get => downloadEta;
        set => Set(ref downloadEta, value);
    }

    public string DownloadSizeText
    {
        get => downloadSizeText;
        set => Set(ref downloadSizeText, value);
    }

    public string DraftTitle
    {
        get => draftTitle;
        set => Set(ref draftTitle, value);
    }

    public string DraftText
    {
        get => draftText;
        set => Set(ref draftText, value);
    }

    public string SelectedVoiceID
    {
        get => selectedVoiceID;
        set => Set(ref selectedVoiceID, value);
    }

    public double Speed
    {
        get => speed;
        set => Set(ref speed, value);
    }

    public AudioFormat Format
    {
        get => format;
        set => Set(ref format, value);
    }

    public DeviceMode DeviceMode
    {
        get => deviceMode;
        set => Set(ref deviceMode, value);
    }

    public string OutputDirectory
    {
        get => outputDirectory;
        set => Set(ref outputDirectory, value);
    }

    public string Status
    {
        get => status;
        set => Set(ref status, value);
    }

    public string? CurrentAudioPath
    {
        get => currentAudioPath;
        set => Set(ref currentAudioPath, value);
    }

    public bool IsGenerating
    {
        get => isGenerating;
        set
        {
            if (Set(ref isGenerating, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GeneratingVisibility)));
            }
        }
    }

    public Visibility GeneratingVisibility => isGenerating ? Visibility.Visible : Visibility.Collapsed;

    public BackendInfo? BackendInfo
    {
        get => backendInfo;
        set
        {
            Set(ref backendInfo, value);
            if (value is not null)
            {
                IsXttsInstalled = value.XttsInstalled;
            }
        }
    }

    public void UpdateFilteredVoices()
    {
        FilteredVoices.Clear();
        var query = (voiceSearchText ?? "").Trim().ToLowerInvariant();
        foreach (var v in Voices)
        {
            if (string.IsNullOrEmpty(query) ||
                v.Name.ToLowerInvariant().Contains(query) ||
                v.Language.ToLowerInvariant().Contains(query) ||
                v.Gender.ToLowerInvariant().Contains(query) ||
                v.Traits.Any(t => t.ToLowerInvariant().Contains(query)))
            {
                FilteredVoices.Add(v);
            }
        }
    }

    public async Task DownloadXttsModelAsync()
    {
        if (IsDownloadingModel) return;

        IsDownloadingModel = true;
        IsDownloadPaused = false;
        DownloadStatus = "Connecting to Hugging Face...";
        downloadCts?.Cancel();
        downloadCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<ModelDownloadProgress>(update =>
            {
                DownloadProgress = update.Percent;
                DownloadStatus = update.Status;
                DownloadSpeed = update.Speed;
                DownloadEta = string.IsNullOrWhiteSpace(update.Eta) ? "" : $"ETA: {update.Eta}";
                DownloadSizeText = update.SizeText;
            });

            await backend.DownloadModelAsync("xtts-v2", progress, downloadCts.Token);
            IsXttsInstalled = true;
            IsDownloadPaused = false;
            DownloadSpeed = "";
            DownloadEta = "";
            DownloadStatus = "XTTS-v2 & multilingual models downloaded successfully!";
            Status = "XTTS-v2 & multilingual models ready.";
        }
        catch (OperationCanceledException)
        {
            IsDownloadPaused = true;
            DownloadSpeed = "";
            DownloadEta = "";
            DownloadStatus = "Download paused (resumable).";
        }
        catch (Exception ex)
        {
            IsDownloadPaused = true;
            DownloadSpeed = "";
            DownloadEta = "";
            DownloadStatus = $"Download stopped: {ex.Message}";
        }
        finally
        {
            IsDownloadingModel = false;
        }
    }

    public void PauseModelDownload()
    {
        downloadCts?.Cancel();
    }

    public async Task StartAsync()
    {
        UpdateFilteredVoices();
        storage.Prepare();
        var settings = await storage.LoadSettingsAsync();
        OutputDirectory = settings.OutputDirectory;
        Format = settings.DefaultFormat;
        Speed = settings.DefaultSpeed;
        SelectedVoiceID = settings.SelectedVoiceID;
        DeviceMode = settings.DeviceMode;

        Projects.Clear();
        foreach (var project in (await storage.LoadProjectsAsync()).OrderByDescending(project => project.UpdatedAt))
        {
            Projects.Add(project);
        }

        try
        {
            BackendInfo = await backend.GetInfoAsync(DeviceMode, CancellationToken.None);
            Status = $"Backend ready on {BackendInfo.SelectedDevice}. ({Voices.Count} voices available)";
        }
        catch (Exception error)
        {
            Status = error.Message;
        }
    }

    public async Task SaveSettingsAsync()
    {
        await storage.SaveSettingsAsync(new AppSettings
        {
            OutputDirectory = OutputDirectory,
            DefaultFormat = Format,
            DefaultSpeed = Speed,
            SelectedVoiceID = SelectedVoiceID,
            DeviceMode = DeviceMode
        });
    }

    public async Task GenerateAsync()
    {
        var cleanText = DraftText.Trim();
        if (cleanText.Length == 0)
        {
            Status = "Enter text before generating speech.";
            return;
        }

        var voice = VoiceCatalog.ById(SelectedVoiceID);
        generationCts?.Cancel();
        generationCts = new CancellationTokenSource();
        IsGenerating = true;
        Status = $"Generating speech with {voice.Name}...";

        try
        {
            await SaveSettingsAsync();
            var title = SafeFilename(string.IsNullOrWhiteSpace(DraftTitle) ? "Atten narration" : DraftTitle);
            var filename = UniqueFilename(title, OutputDirectory, Format);
            var output = await backend.GenerateAsync(
                cleanText,
                SelectedVoiceID,
                Speed,
                Format,
                OutputDirectory,
                filename,
                DeviceMode,
                generationCts.Token);

            var now = DateTimeOffset.Now;
            var project = new ProjectRecord
            {
                Title = title,
                Text = cleanText,
                VoiceID = SelectedVoiceID,
                Speed = Speed,
                Format = Format,
                AudioPath = output.Path,
                CreatedAt = now,
                UpdatedAt = now
            };
            Projects.Insert(0, project);
            await storage.SaveProjectsAsync(Projects);
            CurrentAudioPath = output.Path;
            Status = $"Speech ready! Saved to {Path.GetFileName(output.Path)}";
        }
        catch (OperationCanceledException)
        {
            Status = "Generation cancelled.";
        }
        catch (Exception error)
        {
            Status = $"Error: {error.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    public void CancelGeneration()
    {
        generationCts?.Cancel();
    }

    private static string UniqueFilename(string title, string directory, AudioFormat format)
    {
        Directory.CreateDirectory(directory);
        var candidate = title;
        var counter = 2;
        while (File.Exists(Path.Combine(directory, $"{candidate}.{format}")))
        {
            candidate = $"{title} {counter}";
            counter++;
        }
        return candidate;
    }

    private static string SafeFilename(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return clean.Trim();
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
