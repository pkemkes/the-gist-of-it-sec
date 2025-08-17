using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GistBackend.UnitTest.Utils;

public class LogAsserter(ILogger loggerMock)
{
    public void AssertCorrectErrorLog(EventId logEvent, Exception? exception = null) =>
        AssertCorrectLog(LogLevel.Error, logEvent, exception);

    private void AssertCorrectLog(LogLevel logLevel, EventId logEvent, Exception? exception = null)
    {
        loggerMock.Received(1).Log(
            logLevel,
            logEvent,
            Arg.Any<object>(),
            exception ?? Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception, string>>()!
        );
    }
}
