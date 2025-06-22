using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Handlers.TelegramBotClientHandler;
using GistBackend.Types;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static System.Web.HttpUtility;
using static GistBackend.Utils.LogEvents;
using static GistBackend.Utils.ServiceUtils;

namespace GistBackend.Services;

public class TelegramService(
    IMariaDbHandler mariaDbHandler,
    ITelegramBotClientHandler telegramBotClientHandler,
    IOptions<TelegramServiceOptions> options,
    ILogger<TelegramService>? logger = null
) : BackgroundService
{
    protected CancellationToken? _serviceCancellationToken;
    private static readonly BotCommand StartCommand = new("start", "Register to receive messages");
    private static readonly BotCommand StopCommand = new("stop", "Unregister to stop receiving messages");
    private static readonly List<BotCommand> Commands = [StartCommand, StopCommand];
    private static readonly string AvailableCommands = string.Join(", ", Commands.Select(c => $"/{c.Command}"));

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _serviceCancellationToken = ct;
        telegramBotClientHandler.StartBotClient([StartCommand, StopCommand], OnMessageAsync, OnErrorAsync, ct);
        while (!ct.IsCancellationRequested)
        {
            var startTime = DateTime.UtcNow;
            await ProcessAllChatsAsync(ct);
            await DelayUntilNextExecutionAsync(startTime, 1, null, ct);
        }
    }

    protected async Task OnMessageAsync(Message message, UpdateType updateType)
    {
        if (message.Type != MessageType.Text || message.Text is null)
            return;
        if (message.Text.StartsWith("/"))
        {
            await HandleCommandAsync(message);
        }
        else
        {
            await telegramBotClientHandler.SendMessageAsync(message.Chat.Id,
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
            await telegramBotClientHandler.SendMessageAsync(message.Chat.Id,
                $"Unknown command. Please use one of the following commands: {AvailableCommands}");
        }
    }

    private async Task HandleStartCommandAsync(Message message)
    {
        if (await mariaDbHandler.IsChatRegisteredAsync(message.Chat.Id, _serviceCancellationToken!.Value))
        {
            await telegramBotClientHandler.SendMessageAsync(message.Chat.Id,
                "You are already registered. I will continue to send you gists. Happy reading!");
        }
        else
        {
            await mariaDbHandler.RegisterChatAsync(message.Chat.Id, _serviceCancellationToken!.Value);
            await telegramBotClientHandler.SendMessageAsync(message.Chat.Id,
                "Welcome to The Gist of IT Sec! I registered your chat. " +
                "I will regularly send you gists of the freshest news of selected outlets.");
        }
    }

    private async Task HandleStopCommandAsync(Message message)
    {
        if (await mariaDbHandler.IsChatRegisteredAsync(message.Chat.Id, _serviceCancellationToken!.Value))
        {
            await mariaDbHandler.DeregisterChatAsync(message.Chat.Id, _serviceCancellationToken.Value);
            await telegramBotClientHandler.SendMessageAsync(message.Chat.Id,
                "Such a shame to see you go. I deregistered you. Goodbye.");
        }
        else
        {
            await telegramBotClientHandler.SendMessageAsync(message.Chat.Id,
                "Seems like you were not registered to begin with. I will not send you gists.");
        }
    }

    protected Task OnErrorAsync(Exception exception, HandleErrorSource source)
    {
        logger?.LogError(UnexpectedTelegramError, exception, "An error occurred in the Telegram service: {Source}",
            source);
        return Task.CompletedTask;
    }

    private async Task ProcessAllChatsAsync(CancellationToken ct)
    {
        var chats = await mariaDbHandler.GetAllChatsAsync(ct);
        var gistsToSendByGistIdLastSent = (await Task.WhenAll(
                chats
                    .Select(chat => chat.GistIdLastSent).Distinct()
                    .Select(async id => (id, gists: await mariaDbHandler.GetNextFiveGistsAsync(id, ct))
                )
            ))
            .ToDictionary(tuple => tuple.id, tuple => tuple.gists);
        await Task.WhenAll(chats.Select(chat =>
            SendGistsToChatAsync(chat.Id, gistsToSendByGistIdLastSent[chat.GistIdLastSent], ct)));
    }

    private async Task SendGistsToChatAsync(long chatId, IEnumerable<Gist> gists, CancellationToken ct)
    {
        foreach (var gist in gists) await SendGistToChatAsync(chatId, gist, ct);
    }

    private async Task SendGistToChatAsync(long chatId, Gist gist, CancellationToken ct)
    {
        try
        {
            var gistMessage = await BuildGistMessageAsync(gist, ct);
            await telegramBotClientHandler.SendMessageAsync(chatId, gistMessage, ParseMode.Html);
            await mariaDbHandler.SetGistIdLastSentForChatAsync(chatId, gist.Id!.Value, ct);
            logger?.LogInformation(SentTelegramMessage, "Sent gist {GistId} to chat {ChatId}",
                gist.Id, chatId);
        }
        catch (Exception ex)
        {
            logger?.LogError(SendingGistToChatFailed, ex,
                "Failed to send gist {GistId} to chat {ChatId}", gist.Id, chatId);
        }
    }

    private async Task<string> BuildGistMessageAsync(Gist gist, CancellationToken ct)
    {
        var feed = await mariaDbHandler.GetFeedInfoByIdAsync(gist.FeedId, ct);
        var updatedString = gist.Updated.ToString("dd.MM.YYYY HH:mm 'UTC'");
        var gistUrl = $"{options.Value.AppBaseUrl}/?gist={gist.Id}";
        return $"<b>{HtmlEncode(gist.Title)}</b><br>" +
               $"{HtmlEncode(updatedString)}<br><br>" +
               $"{HtmlEncode(gist.Summary)}<br><br>" +
               $"Tags: <i>{HtmlEncode(string.Join(", ", gist.Tags))}</i><br><br>" +
               $"{HtmlEncode(feed.Title)} - {HtmlEncode(gist.Author)}" +
               $"More details: {HtmlEncode(gistUrl)}";
    }
}
