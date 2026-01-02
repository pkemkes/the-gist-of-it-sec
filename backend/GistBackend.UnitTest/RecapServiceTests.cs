using GistBackend.Handlers;
using GistBackend.Handlers.AIHandler;
using GistBackend.Handlers.MariaDbHandler;
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
        var openAIHandlerMock = Substitute.For<IAIHandler>();
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(DateTime.Parse(dateTimeString));
        var service = CreateRecapService(mariaDbHandlerMock, openAIHandlerMock, dateTimeHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock.DidNotReceive().DailyRecapExistsAsync(Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive().WeeklyRecapExistsAsync(Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .InsertDailyRecapAsync(Arg.Any<RecapAIResponse>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .InsertWeeklyRecapAsync(Arg.Any<RecapAIResponse>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateDailyRecapAsync(Arg.Any<IEnumerable<ConstructedGist>>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateWeeklyRecapAsync(Arg.Any<IEnumerable<ConstructedGist>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_RecapsExist_NoRecapCreated()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.DailyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        mariaDbHandlerMock.WeeklyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        var openAIHandlerMock = Substitute.For<IAIHandler>();
        var service = CreateRecapService(mariaDbHandlerMock, openAIHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock.DidNotReceive()
            .InsertDailyRecapAsync(Arg.Any<RecapAIResponse>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateDailyRecapAsync(Arg.Any<IEnumerable<ConstructedGist>>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .InsertWeeklyRecapAsync(Arg.Any<RecapAIResponse>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateWeeklyRecapAsync(Arg.Any<IEnumerable<ConstructedGist>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_DailyRecapDoesNotExistButNoGists_NoDailyRecapCreated()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.DailyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        mariaDbHandlerMock.WeeklyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        mariaDbHandlerMock.GetConstructedGistsOfLastDayAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ConstructedGist>()));
        var openAIHandlerMock = Substitute.For<IAIHandler>();
        var service = CreateRecapService(mariaDbHandlerMock, openAIHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock.DidNotReceive()
            .InsertDailyRecapAsync(Arg.Any<RecapAIResponse>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateDailyRecapAsync(Arg.Any<IEnumerable<ConstructedGist>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WeeklyRecapDoesNotExistButNoGists_NoWeeklyRecapCreated()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.DailyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        mariaDbHandlerMock.WeeklyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        mariaDbHandlerMock.GetConstructedGistsOfLastWeekAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ConstructedGist>()));
        var openAIHandlerMock = Substitute.For<IAIHandler>();
        var service = CreateRecapService(mariaDbHandlerMock, openAIHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock.DidNotReceive()
            .InsertWeeklyRecapAsync(Arg.Any<RecapAIResponse>(), Arg.Any<CancellationToken>());
        await openAIHandlerMock.DidNotReceive()
            .GenerateWeeklyRecapAsync(Arg.Any<IEnumerable<ConstructedGist>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_DailyRecapDoesNotExist_DailyRecapCreatedAndInserted()
    {
        var testGists = CreateTestConstructedGists(5);
        var testRecap = CreateTestRecap();
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.DailyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        mariaDbHandlerMock.WeeklyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        mariaDbHandlerMock.GetConstructedGistsOfLastDayAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(testGists));
        var aiHandlerMock = Substitute.For<IAIHandler>();
        aiHandlerMock.GenerateDailyRecapAsync(testGists, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(testRecap));
        var service = CreateRecapService(mariaDbHandlerMock, aiHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await aiHandlerMock.Received(1).GenerateDailyRecapAsync(testGists, Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.Received(1).InsertDailyRecapAsync(testRecap, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WeeklyRecapDoesNotExist_WeeklyRecapCreatedAndInserted()
    {
        var testGists = CreateTestConstructedGists(5);
        var testRecap = CreateTestRecap();
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.DailyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        mariaDbHandlerMock.WeeklyRecapExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        mariaDbHandlerMock.GetConstructedGistsOfLastWeekAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(testGists));
        var openAIHandlerMock = Substitute.For<IAIHandler>();
        openAIHandlerMock.GenerateWeeklyRecapAsync(testGists, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(testRecap));
        var service = CreateRecapService(mariaDbHandlerMock, openAIHandlerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await openAIHandlerMock.Received(1).GenerateWeeklyRecapAsync(testGists, Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.Received(1).InsertWeeklyRecapAsync(testRecap, Arg.Any<CancellationToken>());
    }

    private static RecapService CreateRecapService(IMariaDbHandler mariaDbHandlerMock, IAIHandler aiHandlerMock,
        IDateTimeHandler? dateTimeHandlerMock = null)
    {
        if (dateTimeHandlerMock == null)
        {
            dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
            dateTimeHandlerMock.GetUtcNow().Returns(DateTime.Parse("2025-01-01 05:00:00"));
        }
        return new RecapService(mariaDbHandlerMock, aiHandlerMock, dateTimeHandlerMock);
    }
}
