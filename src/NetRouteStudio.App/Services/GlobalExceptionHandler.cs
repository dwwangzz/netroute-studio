using Microsoft.Extensions.Logging;

namespace NetRouteStudio.App.Services;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
{
    public string Handle(Exception exception, string source)
    {
        logger.LogError(exception, "捕获到未处理异常，来源：{Source}", source);
        return "应用遇到异常，请查看日志获取详细信息。";
    }
}
