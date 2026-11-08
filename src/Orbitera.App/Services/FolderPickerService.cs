using Microsoft.Win32;

namespace Orbitera.App.Services;

public sealed class FolderPickerService : IFolderPickerService
{
    public string? PickFolder(string? initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Seleccionar carpeta para Atlas",
            Multiselect = false,
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : null
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
