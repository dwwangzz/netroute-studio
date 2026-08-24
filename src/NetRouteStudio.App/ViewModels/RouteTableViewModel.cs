using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.ViewModels;

public sealed partial class RouteTableViewModel(IRouteTableService routeTableService) : ObservableObject
{
    private const int PageSize = 25;
    private IReadOnlyList<RouteInfo> _allRoutes = [];
    private IReadOnlyList<RouteInfo> _filteredRoutes = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _addressFamilyFilter = "全部";

    [ObservableProperty]
    private int _pageNumber = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private string _statusMessage = "等待读取 Windows 路由表";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<RouteInfo> VisibleRoutes { get; } = [];

    public IReadOnlyList<string> AddressFamilyOptions { get; } = ["全部", "IPv4", "IPv6"];

    [RelayCommand]
    public async Task RefreshRoutesAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = "正在读取 Windows 路由表…";
        try
        {
            _allRoutes = await routeTableService.GetRoutesAsync();
            ApplyFilters();
            StatusMessage = $"共读取 {_allRoutes.Count} 条路由，筛选后 {_filteredRoutes.Count} 条";
        }
        catch (Exception exception)
        {
            _allRoutes = [];
            ErrorMessage = exception.Message;
            StatusMessage = "路由表读取失败";
            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (PageNumber > 1)
        {
            PageNumber--;
            UpdateVisibleRoutes();
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (PageNumber < TotalPages)
        {
            PageNumber++;
            UpdateVisibleRoutes();
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnAddressFamilyFilterChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        IEnumerable<RouteInfo> routes = _allRoutes;
        if (AddressFamilyFilter is "IPv4" or "IPv6")
        {
            var family = AddressFamilyFilter == "IPv4" ? RouteAddressFamily.IPv4 : RouteAddressFamily.IPv6;
            routes = routes.Where(route => route.AddressFamily == family);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            routes = routes.Where(route =>
                route.DestinationPrefix.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                route.NextHop.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                route.InterfaceAlias.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                route.Protocol.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        _filteredRoutes = routes.ToArray();
        PageNumber = 1;
        TotalPages = Math.Max(1, (int)Math.Ceiling(_filteredRoutes.Count / (double)PageSize));
        UpdateVisibleRoutes();
    }

    private void UpdateVisibleRoutes()
    {
        VisibleRoutes.Clear();
        foreach (var route in _filteredRoutes.Skip((PageNumber - 1) * PageSize).Take(PageSize))
        {
            VisibleRoutes.Add(route);
        }
    }
}
