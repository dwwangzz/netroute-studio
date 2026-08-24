namespace NetRouteStudio.App.Models;

public sealed record ControlledCommand(string DisplayCommand, string Executable, IReadOnlyList<string> Arguments);
