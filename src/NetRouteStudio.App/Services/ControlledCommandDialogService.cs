using System.Windows;
using Microsoft.Extensions.DependencyInjection;
namespace NetRouteStudio.App.Services;
public sealed class ControlledCommandDialogService(IServiceProvider provider) : IControlledCommandDialogService
{
    public void Show() { var window = provider.GetRequiredService<ControlledCommandWindow>(); window.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(item => item.IsActive); window.ShowDialog(); }
}
