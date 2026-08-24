using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App;

public partial class BatchRouteManagementWindow : Window
{
    private readonly HashSet<int> _adapterIndexes;

    public BatchRouteManagementWindow(
        IReadOnlyList<RouteInfo> routes,
        IReadOnlyList<NetworkAdapterInfo> adapters)
    {
        InitializeComponent();
        Adapters = adapters;
        _adapterIndexes = adapters.Select(adapter => adapter.InterfaceIndex).ToHashSet();
        foreach (var route in routes)
        {
            var item = BatchRouteEditItem.FromRoute(route);
            item.SelectedAdapter = adapters.FirstOrDefault(adapter => adapter.InterfaceIndex == route.InterfaceIndex);
            Items.Add(item);
        }
        DataContext = this;
    }

    public ObservableCollection<BatchRouteEditItem> Items { get; } = [];

    public IReadOnlyList<NetworkAdapterInfo> Adapters { get; }

    public IReadOnlyList<OperationOption> OperationOptions { get; } =
    [
        new("新增", BatchRouteOperation.Create),
        new("修改", BatchRouteOperation.Update),
        new("删除", BatchRouteOperation.Delete)
    ];

    public IReadOnlyList<BatchRouteEditItem> SelectedItems { get; private set; } = [];

    private void OnAddBlank(object sender, RoutedEventArgs e)
    {
        Items.Insert(0, new BatchRouteEditItem { IsSelected = true, Operation = BatchRouteOperation.Create });
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        try
        {
            var importedItems = BatchRouteTextParser.ParseCreates(ImportTextBox.Text);
            foreach (var item in importedItems)
            {
                if (!int.TryParse(item.InterfaceIndex, out var interfaceIndex))
                {
                    throw new InvalidOperationException($"路由 {item.DestinationPrefix} 的接口索引无效。");
                }

                item.SelectedAdapter = Adapters.FirstOrDefault(adapter => adapter.InterfaceIndex == interfaceIndex)
                    ?? throw new InvalidOperationException(
                        $"路由 {item.DestinationPrefix} 的接口索引 {interfaceIndex} 不存在，请刷新网卡列表后重新导入。");
            }

            foreach (var item in importedItems.Reverse())
            {
                Items.Insert(0, item);
            }
            ImportTextBox.Clear();
            ErrorText.Text = string.Empty;
        }
        catch (Exception exception)
        {
            ErrorText.Text = exception.Message;
        }
    }

    private void OnCopyRow(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: BatchRouteEditItem source })
        {
            return;
        }

        var sourceIndex = Items.IndexOf(source);
        Items.Insert(sourceIndex < 0 ? 0 : sourceIndex + 1, source.CopyAsCreate());
        ErrorText.Text = string.Empty;
    }

    private void OnRemoveRow(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: BatchRouteEditItem item })
        {
            return;
        }

        if (!item.ToggleRemoval())
        {
            Items.Remove(item);
        }
        ErrorText.Text = string.Empty;
    }

    private void OnContinue(object sender, RoutedEventArgs e)
    {
        try
        {
            var selected = Items.Where(item => item.IsSelected).ToArray();
            if (selected.Length == 0)
            {
                throw new InvalidOperationException("请至少勾选一条批量操作记录。");
            }

            foreach (var item in selected)
            {
                if (item.Operation == BatchRouteOperation.Create && item.OriginalRoute is not null)
                {
                    throw new InvalidOperationException($"现有路由 {item.DestinationPrefix} 不能改为新增操作，请添加空白新增行。");
                }
                if (item.Operation != BatchRouteOperation.Create && item.OriginalRoute is null)
                {
                    throw new InvalidOperationException($"新增行 {item.DestinationPrefix} 只能执行新增操作。");
                }
                if (item.Operation != BatchRouteOperation.Delete)
                {
                    if (item.SelectedAdapter is null)
                    {
                        throw new InvalidOperationException($"路由 {item.DestinationPrefix} 尚未选择网络接口。");
                    }

                    var request = item.BuildRequest();
                    if (!_adapterIndexes.Contains(request.InterfaceIndex))
                    {
                        throw new InvalidOperationException(
                            $"路由 {request.DestinationPrefix} 的接口索引 {request.InterfaceIndex} 不存在，请刷新后重新选择。");
                    }
                }
            }

            SelectedItems = selected;
            DialogResult = true;
        }
        catch (Exception exception)
        {
            ErrorText.Text = exception.Message;
        }
    }

    public sealed record OperationOption(string Name, BatchRouteOperation Value);
}
