using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.ViewModels;

public sealed partial class RouteMatchViewModel(IRouteMatchService routeMatchService) : ObservableObject
{
    [ObservableProperty] private string _targetAddress = string.Empty;
    [ObservableProperty] private RouteInfo? _matchedRoute;
    [ObservableProperty] private NativeRouteMatch? _nativeRoute;
    [ObservableProperty] private string _decisionReason = "请输入目标 IP 地址开始匹配。";
    [ObservableProperty] private string _verificationMessage = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private RouteMatchResult? _selectedMatch;

    public ObservableCollection<RouteCandidate> Candidates { get; } = [];
    public ObservableCollection<RouteMatchResult> ResolvedMatches { get; } = [];

    [RelayCommand]
    private async Task MatchAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        Candidates.Clear();
        ResolvedMatches.Clear();
        try
        {
            var result = await routeMatchService.MatchInputAsync(TargetAddress);
            foreach (var match in result.Matches)
            {
                ResolvedMatches.Add(match);
            }

            SelectedMatch = ResolvedMatches.FirstOrDefault();
        }
        catch (Exception exception)
        {
            MatchedRoute = null;
            NativeRoute = null;
            ErrorMessage = exception.Message;
            DecisionReason = "匹配失败。";
            VerificationMessage = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedMatchChanged(RouteMatchResult? value)
    {
        Candidates.Clear();
        if (value is null)
        {
            MatchedRoute = null;
            NativeRoute = null;
            return;
        }

        foreach (var candidate in value.Candidates)
        {
            Candidates.Add(candidate);
        }

        MatchedRoute = value.MatchedRoute;
        NativeRoute = value.NativeRoute;
        DecisionReason = value.DecisionReason;
        VerificationMessage = !value.NativeRoute.IsAvailable
            ? $"Windows 原生查询不可用：{value.NativeRoute.ErrorMessage}"
            : value.IsNativeMatch
            ? "程序计算结果与 Windows 原生查询一致"
            : "程序计算结果与 Windows 原生查询不一致，请检查路由表变化";
    }
}
