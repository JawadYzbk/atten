using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Atten.Windows;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel model = new();
    private readonly MediaPlayer player = new();
    private readonly DispatcherTimer playbackTimer = new();
    private bool isUserSeeking;

    public MainWindow()
    {
        InitializeComponent();
        Root.DataContext = model;

        player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
        player.MediaEnded += OnMediaEnded;

        playbackTimer.Interval = TimeSpan.FromMilliseconds(200);
        playbackTimer.Tick += OnPlaybackTimerTick;
        playbackTimer.Start();

        _ = model.StartAsync();
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        StudioPanel.Visibility = tag == "Studio" ? Visibility.Visible : Visibility.Collapsed;
        PlaygroundPanel.Visibility = tag == "Playground" ? Visibility.Visible : Visibility.Collapsed;
        VoicesPanel.Visibility = tag == "Voices" ? Visibility.Visible : Visibility.Collapsed;
        ProjectsPanel.Visibility = tag == "Projects" ? Visibility.Visible : Visibility.Collapsed;
        ExportsPanel.Visibility = tag == "Exports" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tag == "Settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnGenerateClicked(object sender, RoutedEventArgs args)
    {
        GenerateButton.IsEnabled = false;
        try
        {
            await model.GenerateAsync();
            if (!string.IsNullOrWhiteSpace(model.CurrentAudioPath) && File.Exists(model.CurrentAudioPath))
            {
                PlayCurrentOutput();
            }
        }
        finally
        {
            GenerateButton.IsEnabled = true;
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs args)
    {
        model.CancelGeneration();
    }

    private void OnPlayClicked(object sender, RoutedEventArgs args)
    {
        PlayCurrentOutput();
    }

    private void OnTogglePlayPauseClicked(object sender, RoutedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(model.CurrentAudioPath) || !File.Exists(model.CurrentAudioPath))
        {
            return;
        }

        if (player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            player.Pause();
            model.IsPlaying = false;
        }
        else
        {
            if (player.Source is null)
            {
                player.Source = MediaSource.CreateFromUri(new Uri(model.CurrentAudioPath));
            }
            player.Play();
            model.IsPlaying = true;
        }
    }

    private void OnPlayerSeekValueChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        if (isUserSeeking && player.PlaybackSession.CanSeek)
        {
            player.PlaybackSession.Position = TimeSpan.FromSeconds(args.NewValue);
        }
    }

    private void OnPlayerSeekPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        isUserSeeking = true;
    }

    private void OnPlayerSeekPointerCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        isUserSeeking = false;
    }

    private void OnClosePlayerClicked(object sender, RoutedEventArgs args)
    {
        player.Pause();
        model.IsPlaying = false;
        model.IsPlayerVisible = false;
    }

    private void OnRevealClicked(object sender, RoutedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(model.CurrentAudioPath) || !File.Exists(model.CurrentAudioPath))
        {
            model.Status = "No generated audio is available to reveal.";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{model.CurrentAudioPath}\"",
            UseShellExecute = true
        });
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            model.IsPlaying = sender.PlaybackState == MediaPlaybackState.Playing;
        });
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            model.IsPlaying = false;
            model.PlayerPosition = 0;
            model.PlayerTimeText = $"00:00 / {FormatTime(sender.PlaybackSession.NaturalDuration.TotalSeconds)}";
        });
    }

    private void OnPlaybackTimerTick(object? sender, object e)
    {
        if (player.Source is null) return;

        var session = player.PlaybackSession;
        var duration = session.NaturalDuration.TotalSeconds;
        var position = session.Position.TotalSeconds;

        if (duration > 0)
        {
            model.PlayerDuration = duration;
            if (!isUserSeeking)
            {
                model.PlayerPosition = position;
            }
            model.PlayerTimeText = $"{FormatTime(position)} / {FormatTime(duration)}";
        }
    }

    private static string FormatTime(double totalSeconds)
    {
        if (double.IsNaN(totalSeconds) || totalSeconds < 0) totalSeconds = 0;
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.Hours > 0 ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}" : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private async void OnDownloadXttsClicked(object sender, RoutedEventArgs args)
    {
        if (model.IsDownloadingModel) return;

        DownloadXttsButton.IsEnabled = false;
        XttsProgressBar.Visibility = Visibility.Visible;
        XttsMetricsGrid.Visibility = Visibility.Visible;
        PauseXttsButton.Visibility = Visibility.Visible;
        PauseXttsButton.Content = "Pause";

        try
        {
            await model.DownloadXttsModelAsync();
            if (model.IsXttsInstalled)
            {
                DownloadXttsButton.Content = "Installed";
                DownloadXttsButton.IsEnabled = false;
                PauseXttsButton.Visibility = Visibility.Collapsed;
                XttsMetricsGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                DownloadXttsButton.Content = "Resume Download";
                DownloadXttsButton.IsEnabled = true;
                PauseXttsButton.Content = "Resume";
            }
        }
        finally
        {
            if (!model.IsXttsInstalled)
            {
                DownloadXttsButton.Content = "Resume Download";
                DownloadXttsButton.IsEnabled = true;
                PauseXttsButton.Content = "Resume";
            }
        }
    }

    private async void OnPauseXttsClicked(object sender, RoutedEventArgs args)
    {
        if (model.IsDownloadingModel)
        {
            model.PauseModelDownload();
            PauseXttsButton.Content = "Resume";
            DownloadXttsButton.Content = "Resume Download";
            DownloadXttsButton.IsEnabled = true;
        }
        else
        {
            OnDownloadXttsClicked(sender, args);
        }
    }

    private async void OnRefreshHfModelsClicked(object sender, RoutedEventArgs args)
    {
        await model.FetchHfModelsAsync();
    }

    private void PlayCurrentOutput()
    {
        if (string.IsNullOrWhiteSpace(model.CurrentAudioPath) || !File.Exists(model.CurrentAudioPath))
        {
            model.Status = "No generated audio is available to play.";
            return;
        }

        player.Source = MediaSource.CreateFromUri(new Uri(model.CurrentAudioPath));
        player.Play();
        model.IsPlaying = true;
        model.IsPlayerVisible = true;
    }
}
