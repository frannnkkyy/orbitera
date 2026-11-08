namespace Orbitera.App.Services;

public interface IFolderPickerService
{
    string? PickFolder(string? initialDirectory);
}
