using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly INetworkAdapterService _networkAdapterService;

    public MainWindowViewModel(
        IAdministratorPrivilegeService privilegeService,
        INetworkAdapterService networkAdapterService)
    {
        _networkAdapterService = networkAdapterService;
        IsRunningAsAdministrator = privilegeService.IsRunningAsAdministrator();
        PrivilegeStatus = IsRunningAsAdministrator
            ? "管理员权限：已获取"
            : "管理员权限：未获取";
        StatusMessage = IsRunningAsAdministrator
            ? "应用基础模块已就绪"
            : "网络修改功能需要管理员权限";
    }

    public string ApplicationName => "NetRoute Studio";

    public string ApplicationVersion
    {
        get
        {
            var version = typeof(MainWindowViewModel).Assembly.GetName().Version;
            return version is null ? "未知" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public string ApplicationVersionDisplay => $"版本 {ApplicationVersion}";

    public bool IsRunningAsAdministrator { get; }

    public string PrivilegeStatus { get; }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private NetworkAdapterInfo? _selectedAdapter;

    public ObservableCollection<NetworkAdapterInfo> Adapters { get; } = [];

    [RelayCommand]
    public async Task RefreshAdaptersAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = "正在读取 Windows 网卡信息…";

        try
        {
            var adapters = await _networkAdapterService.GetAdaptersAsync();
            Adapters.Clear();
            foreach (var adapter in adapters)
            {
                Adapters.Add(adapter);
            }

            SelectedAdapter = Adapters.FirstOrDefault();
            StatusMessage = $"已读取 {Adapters.Count} 个网络适配器";
        }
        catch (Exception exception)
        {
            Adapters.Clear();
            SelectedAdapter = null;
            StatusMessage = "网卡信息读取失败";
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
