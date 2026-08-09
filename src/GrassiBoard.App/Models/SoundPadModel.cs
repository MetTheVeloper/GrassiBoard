using System.Text.Json.Serialization;
using GrassiBoard.Infrastructure;

namespace GrassiBoard.Models;

internal sealed class SoundPadModel : ObservableObject
{
    private string _title = "New sound";
    private string _filePath = string.Empty;
    private double _volume = 1.0;
    private bool _loop;
    private bool _restartOnPress = true;
    private bool _isLoaded;
    private bool _isLoading;
    private bool _isPlaying;
    private string? _error;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public double Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, Math.Clamp(value, 0.0, 1.0));
    }

    public bool Loop
    {
        get => _loop;
        set => SetProperty(ref _loop, value);
    }

    public bool RestartOnPress
    {
        get => _restartOnPress;
        set => SetProperty(ref _restartOnPress, value);
    }

    [JsonIgnore]
    public ulong NativeKey
    {
        get
        {
            Span<byte> bytes = stackalloc byte[16];
            Id.TryWriteBytes(bytes);
            ulong key = BitConverter.ToUInt64(bytes);
            return key == 0U ? 1U : key;
        }
    }

    [JsonIgnore]
    public bool IsLoaded
    {
        get => _isLoaded;
        set
        {
            if (SetProperty(ref _isLoaded, value))
            {
                OnPropertyChanged(nameof(StateLabel));
            }
        }
    }

    [JsonIgnore]
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(StateLabel));
            }
        }
    }

    [JsonIgnore]
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(StateLabel));
            }
        }
    }

    [JsonIgnore]
    public string? Error
    {
        get => _error;
        set
        {
            if (SetProperty(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(StateLabel));
            }
        }
    }

    [JsonIgnore]
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    [JsonIgnore]
    public double DurationSeconds { get; set; }

    [JsonIgnore]
    public DateTimeOffset PlaybackStartedAt { get; set; }

    [JsonIgnore]
    public string StateLabel => IsLoading
        ? "Loading..."
        : HasError
            ? "Needs attention"
            : IsPlaying
                ? Loop ? "Playing · Loop" : "Playing"
                : IsLoaded ? "Ready" : "Not loaded";
}
