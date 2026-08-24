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
    IRouteTableService routeTableService,
    INetworkAdapterService networkAdapterService,
    IRouteRestoreComparisonService comparisonService,
    IIPv4RouteManagementService routeManagementService,
    IConfirmationService confirmationService,
    IBatchRouteDialogService resultDialogService,
    ILogger<RouteBackupViewModel> logger) : ObservableObject
{
    private NetworkBackupDocument? _loadedDocument;
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
    [ObservableProperty] private string _hostWarning = string.Empty;

    public ObservableCollection<RouteInfo> Routes { get; } = [];

    public ObservableCollection<NetworkAdapterInfo> Adapters { get; } = [];

    public ObservableCollection<NetworkAdapterInfo> CurrentAdapters { get; } = [];

    public ObservableCollection<RouteRestoreDiffItem> RestoreItems { get; } = [];

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

    [RelayCommand]
    private async Task CompareCurrentAsync()
    {
        if (_loadedDocument is null)
        {
            ErrorMessage = "请先创建或打开一个通过校验的备份文件。";
            return;
        }

        await ExecuteAsync(async () =>
        {
            var routesTask = routeTableService.GetRoutesAsync();
            var adaptersTask = networkAdapterService.GetAdaptersAsync();
            await Task.WhenAll(routesTask, adaptersTask);
            var currentRoutes = await routesTask;
            var currentAdapters = await adaptersTask;

            CurrentAdapters.Clear();
            foreach (var adapter in currentAdapters.OrderBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                CurrentAdapters.Add(adapter);
            }
            RestoreItems.Clear();
            foreach (var item in comparisonService.Compare(_loadedDocument, currentRoutes, currentAdapters))
            {
                RestoreItems.Add(item);
            }

            var actionable = RestoreItems.Count(item => item.CanRestore);
            var selected = RestoreItems.Count(item => item.IsSelected);
            StatusMessage = $"差异比较完成：可恢复 {actionable} 条，默认选中 {selected} 条；当前额外路由不会删除。";
        }, "比较当前 IPv4 路由失败");
    }

    [RelayCommand]
    private async Task RestoreSelectedAsync()
    {
        var selected = RestoreItems.Where(item => item.IsSelected && item.CanRestore).ToArray();
        if (selected.Length == 0)
        {
            ErrorMessage = "请至少勾选一条缺失或配置不同的备份路由。";
            return;
        }

        RouteConfirmationRequest confirmation;
        try
        {
            confirmation = BuildRestoreConfirmation(selected);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            return;
        }
        if (!confirmationService.Confirm(confirmation))
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        var results = new List<BatchRouteExecutionResult>();
        try
        {
            foreach (var item in selected)
            {
                await RestoreItemAsync(item, results);
            }

            var succeeded = results.Count(result => result.Succeeded);
            StatusMessage = $"选择性恢复完成：成功 {succeeded} 条，失败 {results.Count - succeeded} 条；未删除任何额外路由。";
            if (succeeded != results.Count)
            {
                ErrorMessage = "部分路由恢复失败，请查看逐条执行结果。";
            }
        }
        finally
        {
            IsLoading = false;
            resultDialogService.ShowResults(results);
        }
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
        _loadedDocument = document;
        FilePath = path;
        FormatVersion = document.FormatVersion;
        CreatedAt = $"{document.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}（{document.TimeZoneId}）";
        ComputerName = document.ComputerName;
        WindowsVersion = document.WindowsVersion;
        AppVersion = document.AppVersion;
        Sha256 = document.Sha256;
        HostWarning = document.ComputerName.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $"警告：此备份来自计算机 {document.ComputerName}，当前计算机为 {Environment.MachineName}。恢复前必须重新核对接口映射。";

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
        CurrentAdapters.Clear();
        RestoreItems.Clear();
    }

    private RouteConfirmationRequest BuildRestoreConfirmation(IReadOnlyList<RouteRestoreDiffItem> items)
    {
        var fields = new List<RouteConfirmationField>();
        var commands = new List<string>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var request = BuildRestoreRequest(item);
            var operation = item.CurrentRoute is null ? "新增恢复" : "修改恢复";
            var prefix = $"[{index + 1} {operation}] ";
            fields.AddRange(BuildRestoreFields(item, request)
                .Select(field => field with { Name = prefix + field.Name }));
            var command = item.CurrentRoute is null
                ? routeManagementService.GetCreateCommand(request)
                : routeManagementService.GetUpdateCommand(item.CurrentRoute, request);
            commands.Add($"# {prefix}{request.DestinationPrefix}\n{command.Trim()}");
        }

        return new RouteConfirmationRequest(
            "确认选择性恢复 IPv4 路由",
            $"选择性恢复 IPv4 路由（共 {items.Count} 条）",
            fields,
            string.Join("\n\n", commands));
    }

    private static IPv4RouteRequest BuildRestoreRequest(RouteRestoreDiffItem item)
    {
        var backup = item.BackupRoute ?? throw new InvalidOperationException("仅当前存在的路由不能执行备份恢复。");
        var adapter = item.SelectedAdapter ?? throw new InvalidOperationException(
            $"路由 {backup.DestinationPrefix} 未匹配到当前网卡，请从下拉框重新选择接口。");
        return IPv4RouteValidator.ValidateAndNormalize(new IPv4RouteRequest(
            backup.DestinationPrefix,
            backup.NextHop,
            adapter.InterfaceIndex,
            backup.RouteMetric,
            backup.IsPersistent));
    }

    private async Task RestoreItemAsync(
        RouteRestoreDiffItem item,
        ICollection<BatchRouteExecutionResult> results)
    {
        var prefix = item.BackupRoute?.DestinationPrefix ?? "—";
        try
        {
            var request = BuildRestoreRequest(item);
            RouteMutationResult result;
            BatchRouteOperation operation;
            if (item.CurrentRoute is null)
            {
                operation = BatchRouteOperation.Create;
                result = await routeManagementService.CreateAsync(request);
            }
            else
            {
                operation = BatchRouteOperation.Update;
                result = await routeManagementService.UpdateAsync(item.CurrentRoute, request);
            }

            item.CurrentRoute = result.VerifiedRoute;
            item.DifferenceKind = RouteRestoreDifferenceKind.Same;
            item.IsSelected = false;
            results.Add(new BatchRouteExecutionResult(operation, prefix, true, result.Message));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "恢复 IPv4 路由失败：{DestinationPrefix}", prefix);
            var operation = item.CurrentRoute is null ? BatchRouteOperation.Create : BatchRouteOperation.Update;
            results.Add(new BatchRouteExecutionResult(operation, prefix, false, exception.Message));
        }
    }

    private static IReadOnlyList<RouteConfirmationField> BuildRestoreFields(
        RouteRestoreDiffItem item,
        IPv4RouteRequest request)
    {
        var before = item.CurrentRoute;
        var backup = item.BackupRoute!;
        const string empty = "—";
        return
        [
            new("风险属性", before?.OperabilityDisplay ?? empty, item.RiskDisplay),
            new("目标网络", before?.DestinationPrefix ?? empty, request.DestinationPrefix),
            new("下一跳", before?.NextHop ?? empty, request.NextHop),
            new("网络接口", before?.InterfaceAlias ?? empty, item.SelectedAdapter?.Name ?? empty),
            new("接口索引", before?.InterfaceIndex.ToString() ?? empty, request.InterfaceIndex.ToString()),
            new("路由 Metric", before?.RouteMetric.ToString() ?? empty, request.RouteMetric.ToString()),
            new("保存方式", before?.LifetimeDisplay ?? empty, backup.IsPersistent ? "永久" : "临时"),
            new("协议/来源", before?.Protocol ?? empty, backup.Protocol)
        ];
    }
}
