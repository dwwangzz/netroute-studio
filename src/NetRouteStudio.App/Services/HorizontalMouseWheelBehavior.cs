using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NetRouteStudio.App.Services;

public static class HorizontalMouseWheelBehavior
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnPreviewMouseWheel),
            true);
        _registered = true;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer ||
            !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
            scrollViewer.ScrollableWidth <= 0)
        {
            return;
        }

        var targetOffset = Math.Clamp(
            scrollViewer.HorizontalOffset - e.Delta,
            0,
            scrollViewer.ScrollableWidth);
        if (Math.Abs(targetOffset - scrollViewer.HorizontalOffset) < 0.1)
        {
            return;
        }

        scrollViewer.ScrollToHorizontalOffset(targetOffset);
        e.Handled = true;
    }
}
