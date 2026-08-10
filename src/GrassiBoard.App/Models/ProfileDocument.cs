namespace GrassiBoard.Models;

internal sealed class ProfileDocument
{
    public int SchemaVersion { get; set; } = 1;
    public Guid ActiveProfileId { get; set; }
    public List<ProfileModel> Profiles { get; set; } = [];
}
