using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;

namespace Atten.Windows;

public enum AudioFormat
{
    mp3,
    wav
}

public enum DeviceMode
{
    auto,
    cpu,
    cuda,
    mps
}

public sealed record Voice(
    string Id,
    string Name,
    string Language,
    [property: JsonPropertyName("language_code")] string LanguageCode,
    string Gender,
    IReadOnlyList<string> Traits,
    string Quality)
{
    public string ModelEngine => Id.StartsWith("ar_") || Id.StartsWith("de_") || Id.StartsWith("ru_") || Id.StartsWith("tr_") || Id.StartsWith("nl_") || Id.StartsWith("pl_") || Id.StartsWith("xtts_")
        ? "XTTS-v2 & Multilingual Neural"
        : "Kokoro-82M";

    public string ShortName => Name.Contains("(") ? Name.Split('(')[0].Trim() : Name;
    public string DisplayTitle => $"{Name} • {Gender} ({Quality})";
}

public sealed record VoiceGroup(string Language, string ModelEngine, IReadOnlyList<Voice> Voices)
{
    public string Header => $"{Language} • {Voices.Count} voices ({ModelEngine})";
}

public sealed class HfModelInfo : INotifyPropertyChanged
{
    public static readonly Dictionary<string, string> LanguageToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Arabic", "ar" },
        { "English", "en" },
        { "German", "de" },
        { "Spanish", "es" },
        { "French", "fr" },
        { "Italian", "it" },
        { "Portuguese", "pt" },
        { "Russian", "ru" },
        { "Turkish", "tr" },
        { "Dutch", "nl" },
        { "Polish", "pl" },
        { "Japanese", "ja" },
        { "Chinese", "zh" },
        { "Hindi", "hi" },
        { "Korean", "ko" },
        { "Swedish", "sv" }
    };

    public static readonly Dictionary<string, string> CodeToLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ar", "Arabic" },
        { "ara", "Arabic" },
        { "en", "English" },
        { "eng", "English" },
        { "de", "German" },
        { "deu", "German" },
        { "es", "Spanish" },
        { "spa", "Spanish" },
        { "fr", "French" },
        { "fra", "French" },
        { "it", "Italian" },
        { "ita", "Italian" },
        { "pt", "Portuguese" },
        { "por", "Portuguese" },
        { "ru", "Russian" },
        { "rus", "Russian" },
        { "tr", "Turkish" },
        { "tur", "Turkish" },
        { "nl", "Dutch" },
        { "nld", "Dutch" },
        { "pl", "Polish" },
        { "pol", "Polish" },
        { "ja", "Japanese" },
        { "jpn", "Japanese" },
        { "zh", "Chinese" },
        { "zho", "Chinese" },
        { "hi", "Hindi" },
        { "hin", "Hindi" },
        { "ko", "Korean" },
        { "kor", "Korean" },
        { "sv", "Swedish" },
        { "swe", "Swedish" }
    };

    private bool isInstalled;
    private bool isDownloading;

    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Author { get; init; } = "";
    public int Downloads { get; init; }
    public int Likes { get; init; }
    public string DownloadsText { get; init; } = "";
    public string LikesText { get; init; } = "";
    public string LanguagesText { get; init; } = "";
    public IReadOnlyList<string> LanguageCodes { get; init; } = [];

    public bool SupportsLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language) || language == "All Languages") return true;

        var code = LanguageToCode.TryGetValue(language, out var c) ? c : language.ToLowerInvariant();
        var langLower = language.ToLowerInvariant();

        // Specific known model language capabilities
        if (Id.Equals("hexgrad/Kokoro-82M", StringComparison.OrdinalIgnoreCase))
        {
            return code is "en" or "es" or "fr" or "it" or "pt" or "ja" or "zh" or "hi";
        }

        if (Id.Equals("coqui/XTTS-v2", StringComparison.OrdinalIgnoreCase))
        {
            return code is "en" or "es" or "fr" or "de" or "it" or "pt" or "pl" or "tr" or "ru" or "nl" or "cs" or "ar" or "zh" or "ja" or "hu" or "ko" or "hi";
        }

        if (Id.Contains("mms-tts-", StringComparison.OrdinalIgnoreCase))
        {
            return Id.EndsWith($"-{code}", StringComparison.OrdinalIgnoreCase) ||
                   (code == "ar" && Id.EndsWith("-ara", StringComparison.OrdinalIgnoreCase)) ||
                   (code == "de" && Id.EndsWith("-deu", StringComparison.OrdinalIgnoreCase)) ||
                   (code == "ru" && Id.EndsWith("-rus", StringComparison.OrdinalIgnoreCase));
        }

        // Check if explicit tag matches
        if (LanguageCodes.Any(t => t.Equals(code, StringComparison.OrdinalIgnoreCase) || 
                                   t.Equals(langLower, StringComparison.OrdinalIgnoreCase) ||
                                   (code == "ar" && t.Equals("ara", StringComparison.OrdinalIgnoreCase)) ||
                                   (code == "de" && t.Equals("deu", StringComparison.OrdinalIgnoreCase)) ||
                                   (code == "ru" && t.Equals("rus", StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        return LanguagesText.ToLowerInvariant().Contains(langLower);
    }

    public bool IsInstalled
    {
        get => isInstalled;
        set
        {
            if (isInstalled != value)
            {
                isInstalled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstalled)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstallButtonVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstalledBadgeVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadButtonEnabled)));
            }
        }
    }

    public bool IsDownloading
    {
        get => isDownloading;
        set
        {
            if (isDownloading != value)
            {
                isDownloading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDownloading)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadButtonEnabled)));
            }
        }
    }

    public bool DownloadButtonEnabled => !isDownloading && !isInstalled;
    public Visibility InstallButtonVisibility => isInstalled ? Visibility.Collapsed : Visibility.Visible;
    public Visibility InstalledBadgeVisibility => isInstalled ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record ProjectRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = "Untitled narration";
    public string Text { get; set; } = "";
    public string VoiceID { get; set; } = "af_heart";
    public double Speed { get; set; } = 1.0;
    public AudioFormat Format { get; set; } = AudioFormat.mp3;
    public string AudioPath { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public bool IsLegacyImport { get; set; }
}

public sealed record AppSettings
{
    public string OutputDirectory { get; set; } = "";
    public AudioFormat DefaultFormat { get; set; } = AudioFormat.mp3;
    public double DefaultSpeed { get; set; } = 1.0;
    public string SelectedVoiceID { get; set; } = "af_heart";
    public DeviceMode DeviceMode { get; set; } = DeviceMode.auto;
    public HashSet<string> FavoriteVoiceIDs { get; set; } = ["af_heart", "af_bella", "bf_emma"];
}

public sealed record BackendInfo
{
    [JsonPropertyName("selected_device")]
    public string SelectedDevice { get; init; } = "cpu";

    [JsonPropertyName("requested_device")]
    public string RequestedDevice { get; init; } = "auto";

    [JsonPropertyName("torch_version")]
    public string? TorchVersion { get; init; }

    [JsonPropertyName("cuda_available")]
    public bool CudaAvailable { get; init; }

    [JsonPropertyName("cuda_version")]
    public string? CudaVersion { get; init; }

    [JsonPropertyName("mps_available")]
    public bool MpsAvailable { get; init; }

    [JsonPropertyName("model_root_valid")]
    public bool ModelRootValid { get; init; }

    [JsonPropertyName("xtts_installed")]
    public bool XttsInstalled { get; init; }

    [JsonPropertyName("voice_count")]
    public int VoiceCount { get; init; }
}

public sealed record GenerationOutput(string Path, int Segments, int SampleRate);

public sealed record ModelDownloadProgress(
    int Percent,
    string Status,
    string Speed,
    string Eta,
    string SizeText);

public sealed class InstalledModelItem : INotifyPropertyChanged
{
    private bool isInstalled;
    private bool isDownloading;
    private int downloadProgress;
    private string downloadSpeed = "";
    private string downloadEta = "";
    private string downloadSize = "";
    private string downloadStatus = "";

    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string SupportedLanguages { get; init; } = "";
    public bool IsBundled { get; init; }

    public bool IsInstalled
    {
        get => isInstalled;
        set
        {
            if (isInstalled != value)
            {
                isInstalled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstalled)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstalledBadgeVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadButtonVisibility)));
            }
        }
    }

    public bool IsDownloading
    {
        get => isDownloading;
        set
        {
            if (isDownloading != value)
            {
                isDownloading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDownloading)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadingVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadButtonVisibility)));
            }
        }
    }

    public int DownloadProgress
    {
        get => downloadProgress;
        set
        {
            if (downloadProgress != value)
            {
                downloadProgress = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadProgress)));
            }
        }
    }

    public string DownloadSpeed
    {
        get => downloadSpeed;
        set
        {
            if (downloadSpeed != value)
            {
                downloadSpeed = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadSpeed)));
            }
        }
    }

    public string DownloadEta
    {
        get => downloadEta;
        set
        {
            if (downloadEta != value)
            {
                downloadEta = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadEta)));
            }
        }
    }

    public string DownloadSize
    {
        get => downloadSize;
        set
        {
            if (downloadSize != value)
            {
                downloadSize = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadSize)));
            }
        }
    }

    public string DownloadStatus
    {
        get => downloadStatus;
        set
        {
            if (downloadStatus != value)
            {
                downloadStatus = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadStatus)));
            }
        }
    }

    public Visibility InstalledBadgeVisibility => IsInstalled ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DownloadButtonVisibility => (!IsInstalled && !IsDownloading) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DownloadingVisibility => IsDownloading ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;
}
