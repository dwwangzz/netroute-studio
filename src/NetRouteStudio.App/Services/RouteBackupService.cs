using System.Reflection;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed class RouteBackupService(
    IRouteTableService routeTableService,
    INetworkAdapterService networkAdapterService) : IRouteBackupService
{
    public const string CurrentFormatVersion = "1.0";

    private static readonly JsonSerializerOptions CompactJsonOptions = CreateJsonOptions(false);
    private static readonly JsonSerializerOptions IndentedJsonOptions = CreateJsonOptions(true);

    public async Task<RouteBackupResult> CreateAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ValidatePath(filePath);
        var routesTask = routeTableService.GetRoutesAsync(cancellationToken);
        var adaptersTask = networkAdapterService.GetAdaptersAsync(cancellationToken);
        await Task.WhenAll(routesTask, adaptersTask);

        var routes = (await routesTask)
            .Where(route => route.AddressFamily == RouteAddressFamily.IPv4)
            .OrderBy(route => route.DestinationPrefix, StringComparer.OrdinalIgnoreCase)
            .ThenBy(route => route.InterfaceIndex)
            .ThenBy(route => route.NextHop, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var adapters = (await adaptersTask)
            .OrderBy(adapter => adapter.InterfaceIndex)
            .ToArray();
        var payload = new BackupPayload(
            CurrentFormatVersion,
            DateTimeOffset.Now,
            TimeZoneInfo.Local.Id,
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            routes.Length,
            adapters.Length,
            routes,
            adapters);
        var document = ToDocument(payload, ComputeHash(payload));

        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            var json = JsonSerializer.Serialize(document, IndentedJsonOptions);
            await File.WriteAllTextAsync(temporaryPath, json, new UTF8Encoding(false), cancellationToken);
            _ = await LoadAsync(temporaryPath, cancellationToken);
            File.Move(temporaryPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return new RouteBackupResult(fullPath, document);
    }

    public async Task<NetworkBackupDocument> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("找不到指定的路由备份文件。", fullPath);
        }

        await using var stream = File.OpenRead(fullPath);
        var document = await JsonSerializer.DeserializeAsync<NetworkBackupDocument>(
            stream, IndentedJsonOptions, cancellationToken)
            ?? throw new InvalidDataException("备份文件为空或 JSON 结构无效。");
        if (document.FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"不支持的备份格式版本 {document.FormatVersion}，当前支持 {CurrentFormatVersion}。");
        }

        var payload = ToPayload(document);
        var actualHash = ComputeHash(payload);
        if (string.IsNullOrWhiteSpace(document.Sha256) || document.Sha256.Length != 64)
        {
            throw new InvalidDataException("备份文件缺少有效的 SHA-256 校验摘要。");
        }
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(actualHash),
                Encoding.ASCII.GetBytes(document.Sha256 ?? string.Empty)))
        {
            throw new InvalidDataException("备份文件 SHA-256 校验失败，文件可能已损坏或被修改。");
        }

        if (document.RouteCount != document.Routes.Count || document.AdapterCount != document.Adapters.Count)
        {
            throw new InvalidDataException("备份文件中的数量摘要与实际内容不一致。");
        }

        return document;
    }

    public static string GetDefaultFileName() =>
        $"netroute-{SanitizeFileName(Environment.MachineName)}-{DateTime.Now:yyyyMMdd-HHmmss}.json";

    private static string ValidatePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("备份文件路径不能为空。", nameof(filePath));
        }

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("备份文件所在目录不存在。");
        }
        return fullPath;
    }

    private static string ComputeHash(BackupPayload payload)
    {
        var json = JsonSerializer.Serialize(payload, CompactJsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static NetworkBackupDocument ToDocument(BackupPayload payload, string sha256) => new(
        payload.FormatVersion, payload.CreatedAt, payload.TimeZoneId, payload.ComputerName,
        payload.WindowsVersion, payload.AppVersion, payload.RouteCount, payload.AdapterCount,
        payload.Routes, payload.Adapters, sha256);

    private static BackupPayload ToPayload(NetworkBackupDocument document) => new(
        document.FormatVersion, document.CreatedAt, document.TimeZoneId, document.ComputerName,
        document.WindowsVersion, document.AppVersion, document.RouteCount, document.AdapterCount,
        document.Routes, document.Adapters);

    private static JsonSerializerOptions CreateJsonOptions(bool writeIndented)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }

    private sealed record BackupPayload(
        string FormatVersion,
        DateTimeOffset CreatedAt,
        string TimeZoneId,
        string ComputerName,
        string WindowsVersion,
        string AppVersion,
        int RouteCount,
        int AdapterCount,
        IReadOnlyList<RouteInfo> Routes,
        IReadOnlyList<NetworkAdapterInfo> Adapters);
}
