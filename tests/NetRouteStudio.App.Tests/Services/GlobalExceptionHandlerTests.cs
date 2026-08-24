using FluentAssertions;
using Microsoft.Extensions.Logging;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public void 处理异常_应写入错误日志并返回统一提示()
    {
        var logger = new RecordingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);
        var exception = new InvalidOperationException("测试异常");

        var message = handler.Handle(exception, "自动测试");

        message.Should().Be("应用遇到异常，请查看日志获取详细信息。");
        logger.LastLogLevel.Should().Be(LogLevel.Error);
        logger.LastException.Should().BeSameAs(exception);
        logger.LastMessage.Should().Contain("自动测试");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public LogLevel? LastLogLevel { get; private set; }
        public Exception? LastException { get; private set; }
        public string? LastMessage { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LastLogLevel = logLevel;
            LastException = exception;
            LastMessage = formatter(state, exception);
        }
    }
}
