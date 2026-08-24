using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public static class BatchRouteTextParser
{
    public static IReadOnlyList<BatchRouteEditItem> ParseCreates(string text)
    {
        var items = new List<BatchRouteEditItem>();
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < lines.Length; index++)
        {
            var values = lines[index].Split([',', '\t'], StringSplitOptions.TrimEntries);
            if (values.Length != 5)
            {
                throw new FormatException($"第 {index + 1} 行格式错误，应为：目标CIDR,下一跳,接口索引,路由Metric,永久。 ");
            }

            var persistent = values[4] switch
            {
                "true" or "True" or "1" or "永久" => true,
                "false" or "False" or "0" or "临时" => false,
                _ => throw new FormatException($"第 {index + 1} 行的保存方式必须是 true/false、1/0、永久/临时。")
            };
            var item = new BatchRouteEditItem
            {
                IsSelected = true,
                Operation = BatchRouteOperation.Create,
                DestinationPrefix = values[0],
                NextHop = values[1],
                InterfaceIndex = values[2],
                RouteMetric = values[3],
                IsPersistent = persistent
            };
            _ = item.BuildRequest();
            items.Add(item);
        }

        return items;
    }
}
