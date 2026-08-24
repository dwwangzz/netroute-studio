namespace NetRouteStudio.App.Models;

public sealed record ControlledCommandExample(string Category, string Description, string Command)
{
    public string DisplayText => $"{Category}｜{Description}｜{Command}";
}
