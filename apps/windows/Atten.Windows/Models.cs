using System.Text.Json.Serialization;

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

public sealed record HfModelInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Author { get; init; } = "";
    public int Downloads { get; init; }
    public int Likes { get; init; }
    public string DownloadsText { get; init; } = "";
    public string LikesText { get; init; } = "";
    public string LanguagesText { get; init; } = "";
    public bool IsInstalled { get; set; }
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
