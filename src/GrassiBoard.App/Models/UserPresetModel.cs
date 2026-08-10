using GrassiBoard.Infrastructure;

namespace GrassiBoard.Models;

internal sealed class UserPresetModel : ObservableObject
{
    private string _name = "New preset";
    private string _hotkey = string.Empty;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, string.IsNullOrWhiteSpace(value) ? "Untitled preset" : value.Trim());
    }

    public string Hotkey
    {
        get => _hotkey;
        set => SetProperty(ref _hotkey, value?.Trim() ?? string.Empty);
    }

    public AudioStateSnapshot State { get; set; } = new();

    public UserPresetModel Clone(string? name = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name ?? $"{Name} copy",
        Hotkey = string.Empty,
        State = State.Clone()
    };

    public override string ToString() => Name;
}
