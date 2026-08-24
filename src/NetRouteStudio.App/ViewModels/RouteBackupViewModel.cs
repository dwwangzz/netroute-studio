using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.ViewModels;

public sealed partial class RouteBackupViewModel(
    IRouteBackupService backupService,
    IRouteBackupFileDialogService fileDialogService,
    ILogger<RouteBackupViewModel> logger) : ObservableObject
{
    [ObservableProperty] private string _filePath = "尚未创建或打开备份";
    [ObservableProperty] private string _formatVersion = "—";
    [ObservableProperty] private string _createdAt = "—";
    [ObservableProperty] private string _computerName = "—";
    [ObservableProperty] private string _windowsVersion = "—";
    [ObservableProperty] private string _appVersion = "—";
    [ObservableProperty] private string _sha256 = "—";
    [ObservableProperty] private string _statusMessage = "可以创建新的 IPv4 路由备份，或打开已有备份进行校验和预览。";
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<RouteInfo> Routes { get; } = [];

    public ObservableCollection<NetworkAdapterInfo> Adapters { get; } = [];

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        var path = fileDialogService.SelectSavePath(RouteBackupService.GetDefaultFileName());
        if (path is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            var result = await backupService.CreateAsync(path);
            ApplyDocument(result.FilePath, result.Document);
            StatusMessage = $"备份创建成功：{result.Document.RouteCount} 条 IPv4 路由，{result.Document.AdapterCount} 个网卡。";
        }, "创建 IPv4 路由备份失败");
    }

    [RelayCommand]
    private async Task OpenBackupAsync()
    {
        var path = fileDialogService.SelectOpenPath();
        if (path is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            var document = await backupService.LoadAsync(path);
            ApplyDocument(path, document);
            StatusMessage = $"备份校验通过：{document.RouteCount} 条 IPv4 路由，{document.AdapterCount} 个网卡。";
        }, "打开 IPv4 路由备份失败");
    }

    private async Task ExecuteAsync(Func<Task> action, string logMessage)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Operation}", logMessage);
            ErrorMessage = exception.Message;
            StatusMessage = logMessage;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyDocument(string path, NetworkBackupDocument document)
    {
        FilePath = path;
        FormatVersion = document.FormatVersion;
        CreatedAt = $"{document.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}（{document.TimeZoneId}）";
        ComputerName = document.ComputerName;
        WindowsVersion = document.WindowsVersion;
        AppVersion = document.AppVersion;
        Sha256 = document.Sha256;

        Routes.Clear();
        foreach (var route in document.Routes)
        {
            Routes.Add(route);
        }
        Adapters.Clear();
        foreach (var adapter in document.Adapters)
        {
            Adapters.Add(adapter);
        }
    }
}
