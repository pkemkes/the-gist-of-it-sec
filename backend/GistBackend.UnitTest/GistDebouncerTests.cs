using GistBackend.Handlers;
using GistBackend.Utils;
using NSubstitute;

namespace GistBackend.UnitTest;

public class GistDebouncerTests
{
    [Fact]
    public void IsReady_NewGist_False()
    {
        var dateTimeHandler = new DateTimeHandler();
        var debouncer = new GistDebouncer(dateTimeHandler);

        var actual = debouncer.IsReady(1, dateTimeHandler.GetUtcNow() - TimeSpan.FromHours(3));

        Assert.False(actual);
    }

    [Theory]
    [InlineData(          60,      15)]
    [InlineData(      6 * 60,      30)]
    [InlineData( 1 * 24 * 60,      90)]
    [InlineData( 7 * 24 * 60,  3 * 60)]
    [InlineData(14 * 24 * 60, 12 * 60)]
    public void IsReady_CheckingTooEarly_False(int minutes, int minDebounceDurationMinutes)
    {
        const int testGistId = 1;
        var now = DateTime.UtcNow;
        var updated = now - TimeSpan.FromMinutes(minutes / 2);
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(now);
        var debouncer = new GistDebouncer(dateTimeHandlerMock);
        debouncer.IsReady(testGistId, updated); // First call to set the debounce
        dateTimeHandlerMock.GetUtcNow().Returns(now + TimeSpan.FromMinutes(minDebounceDurationMinutes - 1));

        var actual = debouncer.IsReady(testGistId, updated);

        Assert.False(actual);
    }

    [Theory]
    [InlineData(          60,      45)]
    [InlineData(      6 * 60,      90)]
    [InlineData( 1 * 24 * 60,     270)]
    [InlineData( 7 * 24 * 60,  9 * 60)]
    [InlineData(14 * 24 * 60, 36 * 60)]
    public void IsReady_CheckingAfterDebounceDuration_False(int minutes, int maxDebounceDurationMinutes)
    {
        const int testGistId = 1;
        var now = DateTime.UtcNow;
        var updated = now - TimeSpan.FromMinutes(minutes / 2);
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(now);
        var debouncer = new GistDebouncer(dateTimeHandlerMock);
        debouncer.IsReady(testGistId, updated); // First call to set the debounce
        dateTimeHandlerMock.GetUtcNow().Returns(now + TimeSpan.FromMinutes(maxDebounceDurationMinutes + 1));

        var actual = debouncer.IsReady(testGistId, updated);

        Assert.True(actual);
    }
}
