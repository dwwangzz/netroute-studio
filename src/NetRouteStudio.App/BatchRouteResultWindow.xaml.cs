using System.Windows;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App;

public partial class BatchRouteResultWindow : Window
{
    public BatchRouteResultWindow(IReadOnlyList<BatchRouteExecutionResult> results)
    {
        InitializeComponent();
        Results = results;
        Summary = $"共 {results.Count} 条，成功 {results.Count(result => result.Succeeded)} 条，失败 {results.Count(result => !result.Succeeded)} 条。";
        DataContext = this;
    }

    public IReadOnlyList<BatchRouteExecutionResult> Results { get; }

    public string Summary { get; }
}
