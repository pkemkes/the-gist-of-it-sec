using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Handlers.TelegramBotClientHandler;
using GistBackend.Services;
using GistBackend.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TestUtilities;
using Chat = GistBackend.Types.Chat;
using TelegramChat = Telegram.Bot.Types.Chat;

namespace GistBackend.UnitTest;

public class TelegramServiceTests
{
    private class TestableTelegramService : TelegramService
    {
        public TestableTelegramService(IMariaDbHandler mariaDbHandler,
            ITelegramBotClientHandler telegramBotClientHandler,
            IOptions<TelegramServiceOptions> options,
            ILogger<TelegramService>? logger = null) : base(mariaDbHandler, telegramBotClientHandler, options, logger)
        {
            ServiceCancellationToken = CancellationToken.None;
        }

        public Task PublicOnMessageAsync(Message message, UpdateType updateType) => OnMessageAsync(message, updateType);
    }

    [Theory]
    [InlineData("/unknown")]
    [InlineData("just a random message")]
    [InlineData("")]
    public async Task HandleCommandAsync_UnknownCommand_ShouldSendErrorMessage(string command)
    {
        const long expectedChatId = 12345;
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        var telegramBotClientHandlerMock = Substitute.For<ITelegramBotClientHandler>();
        var telegramService = CreateTelegramService(mariaDbHandlerMock, telegramBotClientHandlerMock);
        var message = new Message
        {
            Text = command,
            Chat = new TelegramChat { Id = expectedChatId }
        };

        await telegramService.PublicOnMessageAsync(message, UpdateType.Message);

        await telegramBotClientHandlerMock.Received(1).SendMessageAsync(expectedChatId,
            Arg.Is<string>(s => s.Contains("Please use one of the following commands")));
        await telegramBotClientHandlerMock.DidNotReceive().SendMessageAsync(Arg.Any<long>(),
            Arg.Is<string>(s => !s.Contains("Please use one of the following commands")));
        await mariaDbHandlerMock.DidNotReceive().RegisterChatAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive().DeregisterChatAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleCommandAsync_StartCommandForNewChat_RegistersChatAndSendsWelcomeMessage()
    {
        const long expectedChatId = 12345;
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.IsChatRegisteredAsync(expectedChatId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var telegramBotClientHandlerMock = Substitute.For<ITelegramBotClientHandler>();
        var telegramService = CreateTelegramService(mariaDbHandlerMock, telegramBotClientHandlerMock);
        var message = new Message
        {
            Text = "/start",
            Chat = new TelegramChat { Id = expectedChatId }
        };

        await telegramService.PublicOnMessageAsync(message, UpdateType.Message);

        await mariaDbHandlerMock.Received(1).RegisterChatAsync(expectedChatId, Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive().DeregisterChatAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        await telegramBotClientHandlerMock.Received(1).SendMessageAsync(expectedChatId,
            Arg.Is<string>(s => s.StartsWith("Welcome to The Gist of IT Sec")));
        await telegramBotClientHandlerMock.DidNotReceive().SendMessageAsync(Arg.Any<long>(),
            Arg.Is<string>(s => !s.StartsWith("Welcome to The Gist of IT Sec")));
    }

    [Fact]
    public async Task HandleCommandAsync_StartCommandForAlreadyKnownChat_DoesNotRegisterChatAndSendsErrorMessage()
    {
        const long expectedChatId = 12345;
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.IsChatRegisteredAsync(expectedChatId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var telegramBotClientHandlerMock = Substitute.For<ITelegramBotClientHandler>();
        var telegramService = CreateTelegramService(mariaDbHandlerMock, telegramBotClientHandlerMock);
        var message = new Message
        {
            Text = "/start",
            Chat = new TelegramChat { Id = expectedChatId }
        };

        await telegramService.PublicOnMessageAsync(message, UpdateType.Message);

        await mariaDbHandlerMock.DidNotReceive().RegisterChatAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive().DeregisterChatAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        await telegramBotClientHandlerMock.Received(1).SendMessageAsync(expectedChatId,
            Arg.Is<string>(s => s.StartsWith("You are already registered")));
        await telegramBotClientHandlerMock.DidNotReceive().SendMessageAsync(Arg.Any<long>(),
            Arg.Is<string>(s => !s.StartsWith("You are already registered")));
    }

    [Fact]
    public async Task HandleCommandAsync_StopCommandForAlreadyKnownChat_DeregistersChatAndSendsGoodbyeMessage()
    {
        const long expectedChatId = 12345;
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.IsChatRegisteredAsync(expectedChatId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var telegramBotClientHandlerMock = Substitute.For<ITelegramBotClientHandler>();
        var telegramService = CreateTelegramService(mariaDbHandlerMock, telegramBotClientHandlerMock);
        var message = new Message
        {
            Text = "/stop",
            Chat = new TelegramChat { Id = expectedChatId }
        };

        await telegramService.PublicOnMessageAsync(message, UpdateType.Message);

        await mariaDbHandlerMock.Received(1).DeregisterChatAsync(expectedChatId, Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive().RegisterChatAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        await telegramBotClientHandlerMock.Received(1).SendMessageAsync(expectedChatId,
            Arg.Is<string>(s => s.StartsWith("Such a shame to see you go")));
        await telegramBotClientHandlerMock.DidNotReceive().SendMessageAsync(Arg.Any<long>(),
            Arg.Is<string>(s => !s.StartsWith("Such a shame to see you go")));
    }

    [Fact]
    public async Task HandleCommandAsync_StopCommandForNewChat_DoesNotDeregisterChatAndSendsErrorMessage()
    {
        const long expectedChatId = 12345;
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.IsChatRegisteredAsync(expectedChatId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var telegramBotClientHandlerMock = Substitute.For<ITelegramBotClientHandler>();
        var telegramService = CreateTelegramService(mariaDbHandlerMock, telegramBotClientHandlerMock);
        var message = new Message
        {
            Text = "/stop",
            Chat = new TelegramChat { Id = expectedChatId }
        };

        await telegramService.PublicOnMessageAsync(message, UpdateType.Message);

        await mariaDbHandlerMock.DidNotReceive().DeregisterChatAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive().RegisterChatAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        await telegramBotClientHandlerMock.Received(1).SendMessageAsync(expectedChatId,
            Arg.Is<string>(s => s.StartsWith("Seems like you were not registered to begin with")));
        await telegramBotClientHandlerMock.DidNotReceive().SendMessageAsync(Arg.Any<long>(),
            Arg.Is<string>(s => !s.StartsWith("Seems like you were not registered to begin with")));
    }

    [Fact]
    public async Task ExecuteAsync_NoRegisteredChats_NoMessagesSent()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.GetAllChatsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<Chat>()));
        var telegramBotClientHandlerMock = Substitute.For<ITelegramBotClientHandler>();
        var telegramService = CreateTelegramService(mariaDbHandlerMock, telegramBotClientHandlerMock);

        await telegramService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await telegramBotClientHandlerMock.DidNotReceive()
            .SendMessageAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<ParseMode>());
        await mariaDbHandlerMock.DidNotReceive()
            .SetGistIdLastSentForChatAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MultipleRegisteredChats_SendsMessagesToAll()
    {
        List<Chat> chats = [ new(1, 0), new(2, 0), new(3, 0) ];
        var feed = new TestFeedData(feedId: 0);
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock.GetAllChatsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(chats));
        var gistsWithFeed = feed.Gists.Select(g => GistWithFeed.FromGistAndFeed(g, feed.RssFeedInfo)).ToList();
        mariaDbHandlerMock.GetNextFiveGistsWithFeedAsync(0, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(gistsWithFeed));
        var telegramBotClientHandlerMock = Substitute.For<ITelegramBotClientHandler>();
        var telegramService = CreateTelegramService(mariaDbHandlerMock, telegramBotClientHandlerMock);

        await telegramService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var chat in chats)
        {
            foreach (var gist in gistsWithFeed)
            {
                await telegramBotClientHandlerMock.Received(1)
                    .SendMessageAsync(chat.Id, Arg.Is<string>(s => s.Contains(gist.Title)), ParseMode.Html);
                await mariaDbHandlerMock.Received(1)
                    .SetGistIdLastSentForChatAsync(chat.Id, gist.Id, Arg.Any<CancellationToken>());
            }
        }
    }

    private static TestableTelegramService CreateTelegramService(
        IMariaDbHandler? mariaDbHandler = null,
        ITelegramBotClientHandler? telegramBotClientHandler = null)
    {
        var options = Options.Create(new TelegramServiceOptions{ AppBaseUrl = "test-app-base-url" });
        return new TestableTelegramService(
            mariaDbHandler ?? Substitute.For<IMariaDbHandler>(),
            telegramBotClientHandler ?? Substitute.For<ITelegramBotClientHandler>(),
            options);
    }
}
