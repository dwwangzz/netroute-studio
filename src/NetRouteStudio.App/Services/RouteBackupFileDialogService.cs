using Microsoft.Win32;

namespace NetRouteStudio.App.Services;

public sealed class RouteBackupFileDialogService : IRouteBackupFileDialogService
{
    private const string Filter = "NetRoute 路由备份 (*.json)|*.json|所有文件 (*.*)|*.*";

    public string? SelectSavePath(string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存 IPv4 路由备份",
            Filter = Filter,
            FileName = defaultFileName,
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectOpenPath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "打开 IPv4 路由备份",
            Filter = Filter,
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
