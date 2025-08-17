using System.Globalization;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Handlers.TelegramBotClientHandler;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static System.Web.HttpUtility;
using static GistBackend.Utils.LogEvents;
using static GistBackend.Utils.ServiceUtils;

namespace GistBackend.Services;

public class TelegramService : BackgroundService
{
    protected CancellationToken? ServiceCancellationToken;
    private readonly IMariaDbHandler _mariaDbHandler;
    private readonly ITelegramBotClientHandler _telegramBotClientHandler;
    private readonly string _appBaseUrl;
    private readonly ILogger<TelegramService>? _logger;

    public TelegramService(IMariaDbHandler mariaDbHandler,
        ITelegramBotClientHandler telegramBotClientHandler,
        IOptions<TelegramServiceOptions> options,
        ILogger<TelegramService>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(options.Value.AppBaseUrl))
            throw new ArgumentException("App base URL is not set in the options.");
        _mariaDbHandler = mariaDbHandler;
        _telegramBotClientHandler = telegramBotClientHandler;
        _appBaseUrl = options.Value.AppBaseUrl;
        _logger = logger;
    }

    private static readonly BotCommand StartCommand = new("start", "Register to receive messages");
    private static readonly BotCommand StopCommand = new("stop", "Unregister to stop receiving messages");
    private static readonly List<BotCommand> Commands = [StartCommand, StopCommand];
    private static readonly string AvailableCommands = string.Join(", ", Commands.Select(c => $"/{c.Command}"));

    private static readonly Gauge ProcessChatsGauge =
        Metrics.CreateGauge("process_chats_seconds", "Time spent to process all chats");

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        ServiceCancellationToken = ct;
        _telegramBotClientHandler.StartBotClient([StartCommand, StopCommand], OnMessageAsync, OnErrorAsync, ct);
        while (!ct.IsCancellationRequested)
        {
            var startTime = DateTime.UtcNow;
            using (new SelfReportingStopwatch(elapsed => ProcessChatsGauge.Set(elapsed)))
            {
                await ProcessAllChatsAsync(ct);
            }
            await DelayUntilNextExecutionAsync(startTime, 1, null, ct);
        }
    }

    protected async Task OnMessageAsync(Message message, UpdateType updateType)
    {
        if (message.Type != MessageType.Text || message.Text is null)
            return;
        if (message.Text.StartsWith('/'))
        {
            await HandleCommandAsync(message);
        }
        else
        {
            _logger?.LogInformation(TelegramCommandNotRecognized, "Received message without command: {MessageText}",
                message.Text);
            await _telegramBotClientHandler.SendMessageAsync(message.Chat.Id,
                $"Please use one of the following commands to interact with me: {AvailableCommands}");
        }
    }

    private async Task HandleCommandAsync(Message message)
    {
        var command = message.Text![1..];
        if (command == StartCommand.Command) await HandleStartCommandAsync(message);
        else if (command == StopCommand.Command) await HandleStopCommandAsync(message);
        else
        {
            await _telegramBotClientHandler.SendMessageAsync(message.Chat.Id,
                $"Unknown command. Please use one of the following commands: {AvailableCommands}");
        }
    }

    private async Task HandleStartCommandAsync(Message message)
    {
        if (await _mariaDbHandler.IsChatRegisteredAsync(message.Chat.Id, ServiceCancellationToken!.Value))
        {
            _logger?.LogInformation(StartCommandButAlreadyRegistered,
                "Chat {ChatId} tried to register but is already registered", message.Chat.Id);
            await _telegramBotClientHandler.SendMessageAsync(message.Chat.Id,
                "You are already registered. I will continue to send you gists. Happy reading!");
        }
        else
        {
            _logger?.LogInformation(StartCommandForNewChat, "Registering new chat {ChatId}", message.Chat.Id);
            await _mariaDbHandler.RegisterChatAsync(message.Chat.Id, ServiceCancellationToken!.Value);
            await _telegramBotClientHandler.SendMessageAsync(message.Chat.Id,
                "Welcome to The Gist of IT Sec! I registered your chat. " +
                "I will regularly send you gists of the freshest news of selected outlets.");
        }
    }

    private async Task HandleStopCommandAsync(Message message)
    {
        if (await _mariaDbHandler.IsChatRegisteredAsync(message.Chat.Id, ServiceCancellationToken!.Value))
        {
            _logger?.LogInformation(StopCommandForExistingChat, "Deregistering chat {ChatId}", message.Chat.Id);
            await _mariaDbHandler.DeregisterChatAsync(message.Chat.Id, ServiceCancellationToken.Value);
            await _telegramBotClientHandler.SendMessageAsync(message.Chat.Id,
                "Such a shame to see you go. I deregistered you. Goodbye.");
        }
        else
        {
            _logger?.LogInformation(StopCommandButNotRegistered,
                "Chat {ChatId} tried to deregister but is not registered", message.Chat.Id);
            await _telegramBotClientHandler.SendMessageAsync(message.Chat.Id,
                "Seems like you were not registered to begin with. I will not send you gists.");
        }
    }

    private Task OnErrorAsync(Exception exception, HandleErrorSource source)
    {
        _logger?.LogError(UnexpectedTelegramError, exception, "An error occurred in the Telegram service: {Source}",
            source);
        return Task.CompletedTask;
    }

    private async Task ProcessAllChatsAsync(CancellationToken ct)
    {
        var chats = await _mariaDbHandler.GetAllChatsAsync(ct);
        var gistsToSendByGistIdLastSent = new Dictionary<int, List<GistWithFeed>>();
        foreach (var gistId in chats.Select(chat => chat.GistIdLastSent).Distinct())
        {
            gistsToSendByGistIdLastSent[gistId] = await _mariaDbHandler.GetNextFiveGistsWithFeedAsync(gistId, ct);
        }
        foreach (var chat in chats)
        {
            await SendGistsToChatAsync(chat.Id, gistsToSendByGistIdLastSent[chat.GistIdLastSent], ct);
        }
    }

    private async Task SendGistsToChatAsync(long chatId, IEnumerable<GistWithFeed> gists, CancellationToken ct)
    {
        foreach (var gist in gists) await SendGistToChatAsync(chatId, gist, ct);
    }

    private async Task SendGistToChatAsync(long chatId, GistWithFeed gist, CancellationToken ct)
    {
        try
        {
            _logger?.LogInformation(SendingGistToChat, "Sending gist {GistId} to chat {ChatId}", gist.Id, chatId);
            var message = BuildGistMessage(gist);
            await _telegramBotClientHandler.SendMessageAsync(chatId, message, ParseMode.Html);
            await _mariaDbHandler.SetGistIdLastSentForChatAsync(chatId, gist.Id, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(SendingGistToChatFailed, ex,
                "Failed to send gist {GistId} to chat {ChatId}", gist.Id, chatId);
        }
    }

    private string BuildGistMessage(GistWithFeed gist)
    {
        var updatedString = DateTime
            .ParseExact(gist.Updated, "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal).ToString("dd.MM.yyyy HH:mm 'UTC'");
        var gistUrl = $"{_appBaseUrl}/?gist={gist.Id}";
        return $"<b>{HtmlEncode(gist.Title)}</b>\n" +
               $"{HtmlEncode(updatedString)}\n\n" +
               $"{HtmlEncode(gist.Summary)}\n\n" +
               $"Tags: <i>{HtmlEncode(string.Join(", ", gist.Tags))}</i>\n\n" +
               $"{HtmlEncode(gist.FeedTitle)} - {HtmlEncode(gist.Author)}\n" +
               $"More details: {HtmlEncode(gistUrl)}";
    }
}
