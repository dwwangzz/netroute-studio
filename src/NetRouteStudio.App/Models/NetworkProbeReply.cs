namespace NetRouteStudio.App.Models;

public sealed record NetworkProbeReply(string Status, long? RoundtripTime, string? Address, int? TimeToLive, string ErrorMessage)
{
    public bool Succeeded => Status == "Success";
}
