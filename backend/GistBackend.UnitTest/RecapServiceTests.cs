using GistBackend.Handlers;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Handlers.OpenAiHandler;
using GistBackend.Services;
using GistBackend.Types;
using NSubstitute;
using static TestUtilities.TestData;

namespace GistBackend.UnitTest;

public class RecapServiceTests
{
    [Theory]
    [InlineData("2025-01-01 00:00:00")]
    [InlineData("2025-04-01 03:03:03")]
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
            .InsertDailyRecapAsync(Arg.Any<Recap>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .InsertWeeklyRecapAsync(Arg.Any<Recap>(), Arg.Any<CancellationToken>());
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
            .InsertDailyRecapAsync(Arg.Any<Recap>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateDailyRecapAsync(Arg.Any<IEnumerable<Gist>>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .InsertWeeklyRecapAsync(Arg.Any<Recap>(), Arg.Any<CancellationToken>());
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
            .InsertDailyRecapAsync(Arg.Any<Recap>(), Arg.Any<CancellationToken>());
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
            .InsertWeeklyRecapAsync(Arg.Any<Recap>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateWeeklyRecapAsync(Arg.Any<IEnumerable<Gist>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_DailyRecapDoesNotExist_DailyRecapCreatedAndInserted()
    {
        var testGists = CreateTestGists(5);
        var testRecap = CreateTestRecap();
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.DailyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        mariaDbHandlerMock.WeeklyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        mariaDbHandlerMock.GetGistsOfLastDayAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(testGists));
        var openAIHandlerMock = Substitute.For<IOpenAIHandler>();
        openAIHandlerMock.GenerateDailyRecapAsync(testGists, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(testRecap));
        var service = CreateRecapService(mariaDbHandlerMock, openAIHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await openAIHandlerMock.Received(1).GenerateDailyRecapAsync(testGists, Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.Received(1).InsertDailyRecapAsync(testRecap, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WeeklyRecapDoesNotExist_WeeklyRecapCreatedAndInserted()
    {
        var testGists = CreateTestGists(5);
        var testRecap = CreateTestRecap();
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.DailyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        mariaDbHandlerMock.WeeklyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        mariaDbHandlerMock.GetGistsOfLastWeekAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(testGists));
        var openAIHandlerMock = Substitute.For<IOpenAIHandler>();
        openAIHandlerMock.GenerateWeeklyRecapAsync(testGists, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(testRecap));
        var service = CreateRecapService(mariaDbHandlerMock, openAIHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await openAIHandlerMock.Received(1).GenerateWeeklyRecapAsync(testGists, Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.Received(1).InsertWeeklyRecapAsync(testRecap, Arg.Any<CancellationToken>());
    }

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
