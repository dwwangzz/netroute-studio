namespace NetRouteStudio.App.Services;

public interface IRouteBackupFileDialogService
{
    string? SelectSavePath(string defaultFileName);

    string? SelectOpenPath();
}
