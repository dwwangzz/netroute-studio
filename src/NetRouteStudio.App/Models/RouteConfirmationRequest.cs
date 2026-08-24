namespace NetRouteStudio.App.Models;

public sealed record RouteConfirmationRequest(
    string Title,
    string OperationName,
    IReadOnlyList<RouteConfirmationField> Fields,
    string Command);

public sealed record RouteConfirmationField(string Name, string BeforeValue, string AfterValue);
