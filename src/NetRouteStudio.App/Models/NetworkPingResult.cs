namespace NetRouteStudio.App.Models;

public sealed record NetworkPingResult(string Address, int Sequence, string Status, long? RoundtripTime, int? TimeToLive, string ErrorMessage)
{
    public string LatencyDisplay => RoundtripTime is null ? "—" : $"{RoundtripTime} ms";
}
