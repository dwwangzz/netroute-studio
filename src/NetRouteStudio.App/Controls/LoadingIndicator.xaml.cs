using System.Windows;
using System.Windows.Controls;

namespace NetRouteStudio.App.Controls;

public partial class LoadingIndicator : UserControl
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(LoadingIndicator),
        new PropertyMetadata(false, OnIsActiveChanged));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(LoadingIndicator),
        new PropertyMetadata("正在加载…"));

    public LoadingIndicator()
    {
        InitializeComponent();
        UpdateVisibility();
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    private static void OnIsActiveChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs _)
        => ((LoadingIndicator)dependencyObject).UpdateVisibility();

    private void UpdateVisibility() => Visibility = IsActive ? Visibility.Visible : Visibility.Collapsed;
}
