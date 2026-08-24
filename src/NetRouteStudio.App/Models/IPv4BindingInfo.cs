namespace NetRouteStudio.App.Models;

public sealed record IPv4BindingInfo(string AdapterName, string ComponentId, bool Enabled)
{
    public string StatusDisplay => Enabled ? "已启用" : "已禁用";
}
