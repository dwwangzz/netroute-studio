using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Services;
using NetRouteStudio.App.ViewModels;
using Serilog;

namespace NetRouteStudio.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        HorizontalMouseWheelBehavior.Register();
        RegisterGlobalExceptionHandlers();

        try
        {
            _host = CreateHost();
            await _host.StartAsync();
            _host.Services.GetRequiredService<MainWindow>().Show();
        }
        catch (Exception exception)
        {
            ShowFatalError(exception, "应用启动");
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static IHost CreateHost()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetRouteStudio",
            "logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDirectory, "netroute-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IPowerShellProcessRunner, WindowsPowerShellProcessRunner>();
                services.AddSingleton<IPowerShellExecutor, PowerShellExecutor>();
                services.AddSingleton<IAdministratorPrivilegeService, WindowsAdministratorPrivilegeService>();
                services.AddSingleton<INetworkAdapterService, NetworkAdapterService>();
                services.AddSingleton<IRouteTableService, RouteTableService>();
                services.AddSingleton<IRouteMatchService, RouteMatchService>();
                services.AddSingleton<IIPv4RouteManagementService, IPv4RouteManagementService>();
                services.AddSingleton<IConfirmationService, RouteConfirmationService>();
                services.AddSingleton<IBatchRouteDialogService, BatchRouteDialogService>();
                services.AddSingleton<GlobalExceptionHandler>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<RouteTableViewModel>();
                services.AddSingleton<RouteMatchViewModel>();
                services.AddSingleton<RouteManagementViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowError(e.Exception, "UI 线程");
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            ShowFatalError(exception, "应用程序域");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowError(e.Exception, "后台任务");
        e.SetObserved();
    }

    private void ShowError(Exception exception, string source)
    {
        var message = _host?.Services.GetService<GlobalExceptionHandler>()?.Handle(exception, source)
            ?? "应用遇到异常，请查看日志获取详细信息。";
        MessageBox.Show(message, "NetRoute Studio", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void ShowFatalError(Exception exception, string source)
    {
        if (_host?.Services.GetService<GlobalExceptionHandler>() is { } handler)
        {
            MessageBox.Show(handler.Handle(exception, source), "NetRoute Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Log.Fatal(exception, "未处理的致命异常，来源：{Source}", source);
        MessageBox.Show("应用无法继续运行，请查看日志获取详细信息。", "NetRoute Studio", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
