namespace NetRouteStudio.App.Models;

public sealed record TraceRouteHop(int Hop, string Address, string Status, long? RoundtripTime, string ErrorMessage)
{
    public string LatencyDisplay => RoundtripTime is null ? "—" : $"{RoundtripTime} ms";
}
