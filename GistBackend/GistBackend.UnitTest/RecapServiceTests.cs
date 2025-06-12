using GistBackend.Handlers;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Handlers.OpenAiHandler;
using GistBackend.Services;
using GistBackend.Types;
using NSubstitute;
using static GistBackend.UnitTest.Utils.TestData;

namespace GistBackend.UnitTest;

public class RecapServiceTests
{
    [Theory]
    [InlineData("2025-01-01 06:00:00")]
    [InlineData("2025-04-01 03:03:03")]
    [InlineData("2025-05-21 21:03:03")]
    [InlineData("2021-10-28 17:44:55")]
    public async Task StartAsync_NotUtcHourToCreateRecapAt_NoRecapCreated(string dateTimeString)
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        var openAIHandlerMock = Substitute.For<IOpenAIHandler>();
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(DateTime.Parse(dateTimeString));
        var service = CreateRecapService(mariaDbHandlerMock, openAIHandlerMock, dateTimeHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock.DidNotReceive().DailyRecapExistsAsync(Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive().WeeklyRecapExistsAsync(Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .InsertDailyRecapAsync(Arg.Any<IEnumerable<CategoryRecap>>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .InsertWeeklyRecapAsync(Arg.Any<IEnumerable<CategoryRecap>>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateDailyRecapAsync(Arg.Any<IEnumerable<Gist>>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateWeeklyRecapAsync(Arg.Any<IEnumerable<Gist>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_RecapsExist_NoRecapCreated()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.DailyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        mariaDbHandlerMock.WeeklyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        var openAIHandlerMock = Substitute.For<IOpenAIHandler>();
        var service = CreateRecapService(mariaDbHandlerMock, openAIHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock.DidNotReceive()
            .InsertDailyRecapAsync(Arg.Any<IEnumerable<CategoryRecap>>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateDailyRecapAsync(Arg.Any<IEnumerable<Gist>>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .InsertWeeklyRecapAsync(Arg.Any<IEnumerable<CategoryRecap>>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateWeeklyRecapAsync(Arg.Any<IEnumerable<Gist>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_DailyRecapDoesNotExistButNoGists_NoDailyRecapCreated()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.DailyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        mariaDbHandlerMock.WeeklyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        mariaDbHandlerMock.GetGistsOfLastDayAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<Gist>()));
        var openAIHandlerMock = Substitute.For<IOpenAIHandler>();
        var service = CreateRecapService(mariaDbHandlerMock, openAIHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock.DidNotReceive()
            .InsertDailyRecapAsync(Arg.Any<IEnumerable<CategoryRecap>>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateDailyRecapAsync(Arg.Any<IEnumerable<Gist>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WeeklyRecapDoesNotExistButNoGists_NoWeeklyRecapCreated()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.DailyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        mariaDbHandlerMock.WeeklyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        mariaDbHandlerMock.GetGistsOfLastWeekAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<Gist>()));
        var openAIHandlerMock = Substitute.For<IOpenAIHandler>();
        var service = CreateRecapService(mariaDbHandlerMock, openAIHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock.DidNotReceive()
            .InsertWeeklyRecapAsync(Arg.Any<IEnumerable<CategoryRecap>>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateWeeklyRecapAsync(Arg.Any<IEnumerable<Gist>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_DailyRecapDoesNotExist_DailyRecapCreatedAndInserted()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.DailyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        mariaDbHandlerMock.WeeklyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        mariaDbHandlerMock.GetGistsOfLastDayAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(TestGists));
        var openAIHandlerMock = Substitute.For<IOpenAIHandler>();
        openAIHandlerMock.GenerateDailyRecapAsync(TestGists, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TestRecap));
        var service = CreateRecapService(mariaDbHandlerMock, openAIHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await openAIHandlerMock.Received(1).GenerateDailyRecapAsync(TestGists, Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.Received(1).InsertDailyRecapAsync(TestRecap, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WeeklyRecapDoesNotExist_WeeklyRecapCreatedAndInserted()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.DailyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        mariaDbHandlerMock.WeeklyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        mariaDbHandlerMock.GetGistsOfLastWeekAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(TestGists));
        var openAIHandlerMock = Substitute.For<IOpenAIHandler>();
        openAIHandlerMock.GenerateWeeklyRecapAsync(TestGists, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TestRecap));
        var service = CreateRecapService(mariaDbHandlerMock, openAIHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await openAIHandlerMock.Received(1).GenerateWeeklyRecapAsync(TestGists, Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.Received(1).InsertWeeklyRecapAsync(TestRecap, Arg.Any<CancellationToken>());
    }

    private static readonly List<CategoryRecap> TestRecap = [
        new("test heading", "test recap", [1, 2, 3]),
        new("another test heading", "another test recap", [4, 5, 6]),
        new("last test heading", "last test recap", [111, 222, 333])
    ];

    private static RecapService CreateRecapService(IMariaDbHandler mariaDbHandlerMock, IOpenAIHandler openAIHandlerMock,
        IDateTimeHandler? dateTimeHandlerMock = null)
    {
        if (dateTimeHandlerMock == null)
        {
            dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
            dateTimeHandlerMock.GetUtcNow().Returns(DateTime.Parse("2025-01-01 05:00:00"));
        }
        return new RecapService(mariaDbHandlerMock, openAIHandlerMock, dateTimeHandlerMock);
    }
}
