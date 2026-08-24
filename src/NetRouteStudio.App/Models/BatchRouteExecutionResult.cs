namespace NetRouteStudio.App.Models;

public sealed record BatchRouteExecutionResult(
    BatchRouteOperation Operation,
    string DestinationPrefix,
    bool Succeeded,
    string Message)
{
    public string OperationDisplay => Operation switch
    {
        BatchRouteOperation.Create => "新增",
        BatchRouteOperation.Update => "修改",
        _ => "删除"
    };
}
