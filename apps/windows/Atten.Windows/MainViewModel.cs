using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.UI.Xaml;

namespace Atten.Windows;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private static readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly StorageService storage = new();
    private readonly BackendClient backend = new();
    private CancellationTokenSource? generationCts;
    private string draftTitle = "Untitled narration";
    private string draftText = "";
    private string selectedModel = "All Models";
    private string selectedLanguage = "All Languages";
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

    // Player Bar State
    private bool isPlayerVisible;
    private bool isPlaying;
    private double playerPosition;
    private double playerDuration;
    private string playerTimeText = "00:00 / 00:00";
    private string playerTitle = "";

    // HF Models Filter State
    private bool isFetchingHfModels;
    private string selectedHfLanguage = "All Languages";
    private string selectedHfFilter = "All Models";
    private string hfSearchText = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProjectRecord> Projects { get; } = [];
    public IReadOnlyList<Voice> Voices => VoiceCatalog.All;
    public ObservableCollection<Voice> FilteredVoices { get; } = [];
    public ObservableCollection<VoiceGroup> GroupedVoices { get; } = [];
    public ObservableCollection<string> AvailableModels { get; } = ["All Models", "Kokoro-82M", "XTTS-v2 & Multilingual Neural"];
    public ObservableCollection<string> AvailableLanguages { get; } = [];
    public ObservableCollection<Voice> StudioVoices { get; } = [];
    public ObservableCollection<InstalledModelItem> InstalledEngines { get; } = [];
    public ObservableCollection<HfModelInfo> HfModels { get; } = [];
    public ObservableCollection<HfModelInfo> FilteredHfModels { get; } = [];
    public ObservableCollection<string> HfLanguages { get; } = ["All Languages", "Arabic", "English", "German", "Spanish", "French", "Italian", "Portuguese", "Russian", "Turkish", "Dutch", "Polish", "Japanese", "Chinese", "Hindi"];
    public ObservableCollection<string> HfSortOptions { get; } = ["Most Downloads", "Most Stars", "Provider (A-Z)", "Model Name (A-Z)"];
    public ObservableCollection<string> HfFilters { get; } = ["All Models", "Installed", "Available to Download"];
    public IReadOnlyList<AudioFormat> Formats { get; } = Enum.GetValues<AudioFormat>();
    public IReadOnlyList<DeviceMode> DeviceModes { get; } = Enum.GetValues<DeviceMode>();

    private string selectedHfSort = "Most Downloads";

    public string SelectedHfSort
    {
        get => selectedHfSort;
        set
        {
            if (Set(ref selectedHfSort, value))
            {
                UpdateFilteredHfModels();
                _ = FetchHfModelsAsync();
            }
        }
    }

    public string SelectedHfLanguage
    {
        get => selectedHfLanguage;
        set
        {
            if (Set(ref selectedHfLanguage, value))
            {
                UpdateFilteredHfModels();
                _ = FetchHfModelsAsync();
            }
        }
    }

    public string SelectedHfFilter
    {
        get => selectedHfFilter;
        set
        {
            if (Set(ref selectedHfFilter, value))
            {
                UpdateFilteredHfModels();
            }
        }
    }

    public string HfSearchText
    {
        get => hfSearchText;
        set
        {
            if (Set(ref hfSearchText, value))
            {
                UpdateFilteredHfModels();
            }
        }
    }

    public Visibility XttsInstalledVisibility => isXttsInstalled ? Visibility.Visible : Visibility.Collapsed;
    public Visibility XttsNotInstalledVisibility => !isXttsInstalled ? Visibility.Visible : Visibility.Collapsed;
    public Visibility XttsDownloadingVisibility => isDownloadingModel ? Visibility.Visible : Visibility.Collapsed;

    public bool IsPlayerVisible
    {
        get => isPlayerVisible;
        set
        {
            if (Set(ref isPlayerVisible, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlayerVisibility)));
            }
        }
    }

    public Visibility PlayerVisibility => isPlayerVisible ? Visibility.Visible : Visibility.Collapsed;

    public bool IsPlaying
    {
        get => isPlaying;
        set
        {
            if (Set(ref isPlaying, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlayPauseIcon)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlayPauseLabel)));
            }
        }
    }

    public string PlayPauseIcon => isPlaying ? "\uE769" : "\uE768";
    public string PlayPauseLabel => isPlaying ? "Pause" : "Play";

    public double PlayerPosition
    {
        get => playerPosition;
        set => Set(ref playerPosition, value);
    }

    public double PlayerDuration
    {
        get => playerDuration;
        set => Set(ref playerDuration, value);
    }

    public string PlayerTimeText
    {
        get => playerTimeText;
        set => Set(ref playerTimeText, value);
    }

    public string PlayerTitle
    {
        get => playerTitle;
        set => Set(ref playerTitle, value);
    }

    public bool IsFetchingHfModels
    {
        get => isFetchingHfModels;
        set => Set(ref isFetchingHfModels, value);
    }

    public string SelectedModel
    {
        get => selectedModel;
        set
        {
            if (Set(ref selectedModel, value))
            {
                UpdateAvailableLanguages();
                UpdateStudioVoices();
            }
        }
    }

    public string SelectedLanguage
    {
        get => selectedLanguage;
        set
        {
            if (Set(ref selectedLanguage, value))
            {
                UpdateStudioVoices();
            }
        }
    }

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
        set
        {
            if (Set(ref isXttsInstalled, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(XttsInstalledVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(XttsNotInstalledVisibility)));
                var xttsEngine = InstalledEngines.FirstOrDefault(e => e.Id.Contains("xtts", StringComparison.OrdinalIgnoreCase));
                if (xttsEngine is not null)
                {
                    xttsEngine.IsInstalled = value;
                }
                UpdateHfInstalledStatuses();
            }
        }
    }

    public bool IsDownloadingModel
    {
        get => isDownloadingModel;
        set
        {
            if (Set(ref isDownloadingModel, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(XttsDownloadingVisibility)));
            }
        }
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

    public void UpdateAvailableLanguages()
    {
        var current = SelectedLanguage;
        AvailableLanguages.Clear();
        AvailableLanguages.Add("All Languages");

        var query = Voices.AsEnumerable();
        if (SelectedModel != "All Models")
        {
            query = query.Where(v => v.ModelEngine.Contains(SelectedModel, StringComparison.OrdinalIgnoreCase) ||
                                     SelectedModel.Contains(v.ModelEngine, StringComparison.OrdinalIgnoreCase));
        }

        var distinctLanguages = query.Select(v => v.Language).Distinct().OrderBy(l => l);
        foreach (var lang in distinctLanguages)
        {
            AvailableLanguages.Add(lang);
        }

        if (AvailableLanguages.Contains(current))
        {
            SelectedLanguage = current;
        }
        else
        {
            SelectedLanguage = "All Languages";
        }
    }

    public void UpdateStudioVoices()
    {
        StudioVoices.Clear();
        var query = Voices.AsEnumerable();
        if (SelectedModel != "All Models")
        {
            query = query.Where(v => v.ModelEngine.Contains(SelectedModel, StringComparison.OrdinalIgnoreCase) ||
                                     SelectedModel.Contains(v.ModelEngine, StringComparison.OrdinalIgnoreCase));
        }
        if (SelectedLanguage != "All Languages" && !string.IsNullOrEmpty(SelectedLanguage))
        {
            query = query.Where(v => v.Language.Equals(SelectedLanguage, StringComparison.OrdinalIgnoreCase));
        }

        var matching = query.ToList();
        foreach (var v in matching)
        {
            StudioVoices.Add(v);
        }

        if (StudioVoices.Count > 0 && !StudioVoices.Any(v => v.Id == SelectedVoiceID))
        {
            SelectedVoiceID = StudioVoices[0].Id;
        }
    }

    public void UpdateFilteredVoices()
    {
        FilteredVoices.Clear();
        GroupedVoices.Clear();
        var query = (voiceSearchText ?? "").Trim().ToLowerInvariant();

        var matched = Voices.Where(v =>
            string.IsNullOrEmpty(query) ||
            v.Name.ToLowerInvariant().Contains(query) ||
            v.Language.ToLowerInvariant().Contains(query) ||
            v.Gender.ToLowerInvariant().Contains(query) ||
            v.ModelEngine.ToLowerInvariant().Contains(query) ||
            v.Traits.Any(t => t.ToLowerInvariant().Contains(query))).ToList();

        foreach (var v in matched)
        {
            FilteredVoices.Add(v);
        }

        var groups = matched
            .GroupBy(v => (v.Language, v.ModelEngine))
            .OrderBy(g => g.Key.Language)
            .Select(g => new VoiceGroup(g.Key.Language, g.Key.ModelEngine, g.ToList()));

        foreach (var group in groups)
        {
            GroupedVoices.Add(group);
        }
    }

    public void UpdateFilteredHfModels()
    {
        var query = (hfSearchText ?? "").Trim().ToLowerInvariant();
        var list = new List<HfModelInfo>();

        foreach (var m in HfModels)
        {
            // Language filter
            if (SelectedHfLanguage != "All Languages" && !string.IsNullOrEmpty(SelectedHfLanguage))
            {
                if (!m.SupportsLanguage(SelectedHfLanguage))
                {
                    continue;
                }
            }

            // Installed filter
            if (SelectedHfFilter == "Installed" && !m.IsInstalled)
            {
                continue;
            }
            if (SelectedHfFilter == "Available to Download" && m.IsInstalled)
            {
                continue;
            }

            // Search query filter
            if (!string.IsNullOrEmpty(query))
            {
                var matches = m.Name.ToLowerInvariant().Contains(query) ||
                              m.Author.ToLowerInvariant().Contains(query) ||
                              m.Id.ToLowerInvariant().Contains(query) ||
                              m.LanguagesText.ToLowerInvariant().Contains(query);
                if (!matches) continue;
            }

            list.Add(m);
        }

        // Apply sorting
        IEnumerable<HfModelInfo> sorted = SelectedHfSort switch
        {
            "Most Stars" => list.OrderByDescending(m => m.Likes),
            "Provider (A-Z)" => list.OrderBy(m => string.IsNullOrEmpty(m.Author) ? m.Name : m.Author).ThenBy(m => m.Name),
            "Model Name (A-Z)" => list.OrderBy(m => m.Name),
            _ => list.OrderByDescending(m => m.Downloads)
        };

        FilteredHfModels.Clear();
        foreach (var item in sorted)
        {
            FilteredHfModels.Add(item);
        }
    }

    public async Task FetchHfModelsAsync()
    {
        if (IsFetchingHfModels) return;
        IsFetchingHfModels = true;

        try
        {
            var langParam = "";
            if (!string.IsNullOrWhiteSpace(SelectedHfLanguage) && SelectedHfLanguage != "All Languages")
            {
                if (HfModelInfo.LanguageToCode.TryGetValue(SelectedHfLanguage, out var code))
                {
                    langParam = $"&filter={code}";
                }
            }

            var sortParam = SelectedHfSort == "Most Stars" ? "likes" : "downloads";
            var url = $"https://huggingface.co/api/models?pipeline_tag=text-to-speech{langParam}&sort={sortParam}&direction=-1&limit=30&expand[]=safetensors";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Atten/0.2.1");

            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                HfModels.Clear();

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetString() ?? "";
                    var parts = id.Split('/');
                    var author = parts.Length > 1 ? parts[0] : "";
                    var name = parts.Length > 1 ? parts[1] : id;
                    var downloads = item.TryGetProperty("downloads", out var d) ? d.GetInt32() : 0;
                    var likes = item.TryGetProperty("likes", out var l) ? l.GetInt32() : 0;

                    var langCodes = new List<string>();
                    var langNames = new List<string>();
                    if (item.TryGetProperty("tags", out var tags))
                    {
                        foreach (var tag in tags.EnumerateArray())
                        {
                            var t = (tag.GetString() ?? "").ToLowerInvariant();
                            if (t.StartsWith("language:"))
                            {
                                t = t.Replace("language:", "");
                            }
                            if (HfModelInfo.CodeToLanguage.TryGetValue(t, out var langName))
                            {
                                if (!langCodes.Contains(t)) langCodes.Add(t);
                                if (!langNames.Contains(langName)) langNames.Add(langName);
                            }
                        }
                    }

                    string langText;
                    if (langNames.Count > 0)
                    {
                        langText = string.Join(", ", langNames.Take(5));
                        if (langNames.Count > 5) langText += $", +{langNames.Count - 5} more";
                    }
                    else
                    {
                        langText = id.Contains("ara") ? "Arabic" : (id.Contains("deu") ? "German" : "Multilingual");
                    }

                    var downloadsText = downloads >= 1_000_000 ? $"{downloads / 1_000_000.0:F1}M downloads" :
                                        downloads >= 1_000 ? $"{downloads / 1_000.0:F1}K downloads" : $"{downloads} downloads";

                    var likesText = likes >= 1_000 ? $"{likes / 1_000.0:F1}k" : $"{likes}";

                    string sizeText = "";
                    if (item.TryGetProperty("safetensors", out var safetensors) && safetensors.TryGetProperty("total", out var total))
                    {
                        var totalBytes = total.GetInt64();
                        if (totalBytes >= 1_073_741_824)
                            sizeText = $"{totalBytes / 1_073_741_824.0:F1} GB";
                        else if (totalBytes >= 1_048_576)
                            sizeText = $"{totalBytes / 1_048_576.0:F0} MB";
                        else if (totalBytes > 0)
                            sizeText = $"{totalBytes / 1024.0:F0} KB";
                    }

                    if (string.IsNullOrEmpty(sizeText))
                    {
                        if (id.Contains("Kokoro", StringComparison.OrdinalIgnoreCase)) sizeText = "82 MB";
                        else if (id.Contains("XTTS", StringComparison.OrdinalIgnoreCase)) sizeText = "1.87 GB";
                        else if (id.Contains("mms-tts", StringComparison.OrdinalIgnoreCase)) sizeText = "145 MB";
                        else if (id.Contains("1.7B", StringComparison.OrdinalIgnoreCase) || id.Contains("1.5", StringComparison.OrdinalIgnoreCase)) sizeText = "1.7 GB";
                        else if (id.Contains("F5-TTS", StringComparison.OrdinalIgnoreCase)) sizeText = "1.1 GB";
                        else if (id.Contains("chatterbox", StringComparison.OrdinalIgnoreCase)) sizeText = "1.9 GB";
                        else sizeText = "~1.2 GB";
                    }

                    var isInstalled = id.Equals("hexgrad/Kokoro-82M", StringComparison.OrdinalIgnoreCase) ||
                                      (id.Equals("coqui/XTTS-v2", StringComparison.OrdinalIgnoreCase) && IsXttsInstalled) ||
                                      id.Equals("facebook/mms-tts-ara", StringComparison.OrdinalIgnoreCase);

                    HfModels.Add(new HfModelInfo
                    {
                        Id = id,
                        Name = name,
                        Author = author,
                        Downloads = downloads,
                        Likes = likes,
                        DownloadsText = downloadsText,
                        LikesText = likesText,
                        SizeText = sizeText,
                        LanguagesText = langText,
                        LanguageCodes = langCodes,
                        IsInstalled = isInstalled
                    });
                }
            }
        }
        catch
        {
            if (HfModels.Count == 0)
            {
                PopulateFallbackHfModels();
            }
        }
        finally
        {
            if (HfModels.Count == 0)
            {
                PopulateFallbackHfModels();
            }
            UpdateFilteredHfModels();
            IsFetchingHfModels = false;
        }
    }

    private void PopulateFallbackHfModels()
    {
        HfModels.Clear();
        HfModels.Add(new HfModelInfo
        {
            Id = "hexgrad/Kokoro-82M",
            Name = "Kokoro-82M",
            Author = "hexgrad",
            Downloads = 11500000,
            Likes = 6900,
            DownloadsText = "11.5M downloads",
            LikesText = "6.9k",
            SizeText = "82 MB",
            LanguagesText = "English, Spanish, French, Italian, Portuguese, Japanese, Chinese, Hindi",
            LanguageCodes = ["en", "es", "fr", "it", "pt", "ja", "zh", "hi"],
            IsInstalled = true
        });
        HfModels.Add(new HfModelInfo
        {
            Id = "coqui/XTTS-v2",
            Name = "XTTS-v2",
            Author = "coqui",
            Downloads = 7300000,
            Likes = 3800,
            DownloadsText = "7.3M downloads",
            LikesText = "3.8k",
            SizeText = "1.87 GB",
            LanguagesText = "Arabic, German, Russian, Turkish, Dutch, Polish, and 16+ languages",
            LanguageCodes = ["ar", "de", "ru", "tr", "nl", "pl", "es", "fr", "it", "pt", "ja", "zh", "hi", "ko"],
            IsInstalled = IsXttsInstalled
        });
        HfModels.Add(new HfModelInfo
        {
            Id = "facebook/mms-tts-ara",
            Name = "MMS-TTS Arabic",
            Author = "facebook",
            Downloads = 1200000,
            Likes = 1450,
            DownloadsText = "1.2M downloads",
            LikesText = "1.5k",
            SizeText = "145 MB",
            LanguagesText = "Arabic (العربية)",
            LanguageCodes = ["ar", "ara"],
            IsInstalled = true
        });
        HfModels.Add(new HfModelInfo
        {
            Id = "SWivid/F5-TTS",
            Name = "F5-TTS",
            Author = "SWivid",
            Downloads = 950000,
            Likes = 1200,
            DownloadsText = "950K downloads",
            LikesText = "1.2k",
            SizeText = "1.1 GB",
            LanguagesText = "English, Chinese",
            LanguageCodes = ["en", "zh"],
            IsInstalled = false
        });
    }

    private void UpdateHfInstalledStatuses()
    {
        foreach (var m in HfModels)
        {
            if (m.Id.Equals("coqui/XTTS-v2", StringComparison.OrdinalIgnoreCase))
            {
                m.IsInstalled = IsXttsInstalled;
            }
        }
        UpdateFilteredHfModels();
    }

    public void InitializeInstalledEngines()
    {
        InstalledEngines.Clear();
        InstalledEngines.Add(new InstalledModelItem
        {
            Id = "hexgrad/Kokoro-82M",
            Name = "Kokoro-82M (Default Engine)",
            Description = "Bundled offline model (English US/UK, Spanish, French, Italian, Portuguese, Japanese, Mandarin, Hindi)",
            SupportedLanguages = "English, Spanish, French, Italian, Portuguese, Japanese, Chinese, Hindi",
            IsInstalled = true,
            IsBundled = true
        });

        InstalledEngines.Add(new InstalledModelItem
        {
            Id = "coqui/XTTS-v2",
            Name = "XTTS-v2 & Multilingual Neural Models",
            Description = "High-quality neural model with Arabic (العربية), German, Russian, Turkish, Dutch, Polish and 16+ languages",
            SupportedLanguages = "Arabic, German, Russian, Turkish, Dutch, Polish, and 16+ languages",
            IsInstalled = IsXttsInstalled
        });
    }

    private readonly HashSet<string> pendingDownloadModelIds = [];

    public async Task DownloadHfModelAsync(string modelId)
    {
        if (IsDownloadingModel) return;

        pendingDownloadModelIds.Add(modelId);
        _ = SaveSettingsAsync();

        var targetModel = HfModels.FirstOrDefault(m => m.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        if (targetModel is not null)
        {
            targetModel.IsDownloading = true;
        }

        // Add or retrieve entry in InstalledEngines list
        var engine = InstalledEngines.FirstOrDefault(e => e.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        if (engine is null)
        {
            var displayName = targetModel?.Name ?? (modelId.Contains('/') ? modelId.Split('/')[1] : modelId);
            var author = targetModel?.Author ?? (modelId.Contains('/') ? modelId.Split('/')[0] : "");
            var desc = !string.IsNullOrEmpty(author) 
                ? $"{author}/{displayName} • {targetModel?.LanguagesText ?? "Multilingual"}"
                : $"{displayName} • {targetModel?.LanguagesText ?? "Multilingual"}";

            engine = new InstalledModelItem
            {
                Id = modelId,
                Name = displayName,
                Description = desc,
                SupportedLanguages = targetModel?.LanguagesText ?? "Multilingual",
                IsInstalled = false,
                IsDownloading = true,
                IsPaused = false,
                DownloadStatus = $"Resuming / Connecting to Hugging Face for {modelId}..."
            };
            InstalledEngines.Add(engine);
        }
        else
        {
            engine.IsDownloading = true;
            engine.IsPaused = false;
            engine.DownloadStatus = $"Resuming / Connecting to Hugging Face for {modelId}...";
        }

        IsDownloadingModel = true;
        IsDownloadPaused = false;
        DownloadStatus = $"Resuming download for {modelId}...";
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

                engine.DownloadProgress = update.Percent;
                engine.DownloadStatus = update.Status;
                engine.DownloadSpeed = update.Speed;
                engine.DownloadEta = string.IsNullOrWhiteSpace(update.Eta) ? "" : $"ETA: {update.Eta}";
                engine.DownloadSize = update.SizeText;
            });

            await backend.DownloadModelAsync(modelId, progress, downloadCts.Token);

            if (targetModel is not null)
            {
                targetModel.IsInstalled = true;
                targetModel.IsDownloading = false;
            }

            engine.IsInstalled = true;
            engine.IsDownloading = false;
            engine.IsPaused = false;
            engine.DownloadSpeed = "";
            engine.DownloadEta = "";
            engine.DownloadStatus = "Download complete and model ready!";

            if (modelId.Contains("xtts", StringComparison.OrdinalIgnoreCase))
            {
                IsXttsInstalled = true;
            }

            pendingDownloadModelIds.Remove(modelId);
            _ = SaveSettingsAsync();

            IsDownloadPaused = false;
            DownloadSpeed = "";
            DownloadEta = "";
            DownloadStatus = $"{modelId} downloaded successfully!";
            Status = $"{modelId} model ready.";
            UpdateHfInstalledStatuses();
        }
        catch (OperationCanceledException)
        {
            if (targetModel is not null) targetModel.IsDownloading = false;
            engine.IsDownloading = false;
            engine.IsPaused = true;
            engine.DownloadSpeed = "";
            engine.DownloadEta = "";
            engine.DownloadStatus = "Download paused (resumable).";
            IsDownloadPaused = true;
            DownloadSpeed = "";
            DownloadEta = "";
            DownloadStatus = "Download paused (resumable).";
            _ = SaveSettingsAsync();
        }
        catch (Exception ex)
        {
            if (targetModel is not null) targetModel.IsDownloading = false;
            engine.IsDownloading = false;
            engine.IsPaused = true;
            engine.DownloadSpeed = "";
            engine.DownloadEta = "";
            engine.DownloadStatus = $"Download stopped: {ex.Message}";
            IsDownloadPaused = true;
            DownloadSpeed = "";
            DownloadEta = "";
            DownloadStatus = $"Download stopped: {ex.Message}";
            _ = SaveSettingsAsync();
        }
        finally
        {
            IsDownloadingModel = false;
        }
    }

    public async Task DownloadXttsModelAsync()
    {
        await DownloadHfModelAsync("coqui/XTTS-v2");
    }

    public void PauseModelDownload()
    {
        downloadCts?.Cancel();
    }

    public void PauseEngineDownload(string modelId)
    {
        downloadCts?.Cancel();
    }

    public void CancelEngineDownload(string modelId)
    {
        if (IsDownloadingModel)
        {
            downloadCts?.Cancel();
        }

        pendingDownloadModelIds.Remove(modelId);
        _ = SaveSettingsAsync();

        var engine = InstalledEngines.FirstOrDefault(e => e.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        if (engine is not null)
        {
            engine.IsDownloading = false;
            engine.IsPaused = false;
            engine.DownloadSpeed = "";
            engine.DownloadEta = "";
            engine.DownloadStatus = "Download cancelled.";
            if (!engine.IsBundled && !engine.IsInstalled)
            {
                InstalledEngines.Remove(engine);
            }
        }

        var targetModel = HfModels.FirstOrDefault(m => m.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        if (targetModel is not null)
        {
            targetModel.IsDownloading = false;
        }

        IsDownloadPaused = false;
        DownloadSpeed = "";
        DownloadEta = "";
        DownloadStatus = $"Download for {modelId} cancelled.";
        Status = $"Download for {modelId} cancelled.";
    }

    public async Task StartAsync()
    {
        InitializeInstalledEngines();
        UpdateAvailableLanguages();
        UpdateStudioVoices();
        UpdateFilteredVoices();

        storage.Prepare();
        var settings = await storage.LoadSettingsAsync();
        OutputDirectory = settings.OutputDirectory;
        Format = settings.DefaultFormat;
        Speed = settings.DefaultSpeed;
        SelectedVoiceID = settings.SelectedVoiceID;
        DeviceMode = settings.DeviceMode;

        pendingDownloadModelIds.Clear();
        foreach (var id in settings.PendingDownloadModelIds)
        {
            pendingDownloadModelIds.Add(id);
        }

        Projects.Clear();
        foreach (var project in (await storage.LoadProjectsAsync()).OrderByDescending(project => project.UpdatedAt))
        {
            Projects.Add(project);
        }

        try
        {
            BackendInfo = await backend.GetInfoAsync(DeviceMode, CancellationToken.None);
            Status = $"Backend ready on {BackendInfo.SelectedDevice}. ({Voices.Count} voices available across {AvailableLanguages.Count - 1} languages)";
        }
        catch (Exception error)
        {
            Status = error.Message;
        }

        _ = FetchHfModelsAsync();

        // Automatically resume any downloads that were in progress when the app was closed
        if (pendingDownloadModelIds.Count > 0)
        {
            var toResume = pendingDownloadModelIds.ToList();
            foreach (var pendingId in toResume)
            {
                if (pendingId.Contains("xtts", StringComparison.OrdinalIgnoreCase) && (BackendInfo?.XttsInstalled == true || IsXttsInstalled))
                {
                    pendingDownloadModelIds.Remove(pendingId);
                    continue;
                }

                _ = DownloadHfModelAsync(pendingId);
            }
            _ = SaveSettingsAsync();
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
            DeviceMode = DeviceMode,
            PendingDownloadModelIds = pendingDownloadModelIds
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
            PlayerTitle = $"{title}.{Format}";
            IsPlayerVisible = true;
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
