using GistBackend.Handler.MariaDbHandler;
using GistBackend.Types;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static GistBackend.Utils.LogEvents;
using static GistBackend.Utils.ServiceUtils;

namespace GistBackend.Services;

public class TelegramService(
    IMariaDbHandler mariaDbHandler,
    IOptions<TelegramServiceOptions> options,
    ILogger<TelegramService>? logger = null
) : BackgroundService
{
    private CancellationToken? _serviceCancellationToken;
    private TelegramBotClient? _bot;
    private static readonly BotCommand StartCommand = new("start", "Register to receive messages");
    private static readonly BotCommand StopCommand = new("stop", "Unregister to stop receiving messages");
    private static readonly List<BotCommand> Commands = [StartCommand, StopCommand];
    private static readonly string AvailableCommands = string.Join(", ", Commands.Select(c => $"/{c.Command}"));

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _serviceCancellationToken = ct;
        _bot = new TelegramBotClient(options.Value.BotToken, cancellationToken: ct);
        await _bot.SetMyCommands([StartCommand, StopCommand], cancellationToken: ct);
        _bot.OnMessage += OnMessageAsync;
        _bot.OnError += OnErrorAsync;
        while (!ct.IsCancellationRequested)
        {
            var startTime = DateTime.UtcNow;
            await ProcessAllChatsAsync(ct);
            await DelayUntilNextExecutionAsync(startTime, 1, null, ct);
        }
    }

    private async Task OnMessageAsync(Message message, UpdateType updateType)
    {
        if (message.Type != MessageType.Text || message.Text is null)
            return;
        if (message.Text.StartsWith("/"))
        {
            await HandleCommandAsync(message);
        }
        else
        {
            await SendMessageAsync(message.Chat.Id,
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
            await SendMessageAsync(message.Chat.Id,
                $"Unknown command. Please use one of the following commands: {AvailableCommands}");
        }
    }

    private async Task HandleStartCommandAsync(Message message)
    {
        if (await mariaDbHandler.IsChatRegisteredAsync(message.Chat.Id, _serviceCancellationToken!.Value))
        {
            await SendMessageAsync(message.Chat.Id,
                "You are already registered. I will continue to send you gists. Happy reading!");
        }

        await mariaDbHandler.RegisterChatAsync(message.Chat.Id, _serviceCancellationToken!.Value);
        await SendMessageAsync(message.Chat.Id,
            "Welcome to The Gist of IT Sec! I registered your chat. " +
            "I will regularly send you gists of the freshest news of selected outlets.");
    }

    private async Task HandleStopCommandAsync(Message message)
    {
        if (await mariaDbHandler.IsChatRegisteredAsync(message.Chat.Id, _serviceCancellationToken!.Value))
        {
            await mariaDbHandler.DeregisterChatAsync(message.Chat.Id, _serviceCancellationToken.Value);
            await SendMessageAsync(message.Chat.Id, "Such a shame to see you go. I deregistered you. Goodbye.");
        }
        else
        {
            await SendMessageAsync(message.Chat.Id,
                "Seems like you were not registered to begin with. I will not send you gists.");
        }
    }

    private Task OnErrorAsync(Exception exception, HandleErrorSource source)
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
                    .Select(async id => (id, gists: await mariaDbHandler.GetNextGistsAsync(id, ct))
                )
            ))
            .ToDictionary(tuple => tuple.id, tuple => tuple.gists);
        await Task.WhenAll(chats.Select(chat =>
            SendGistsToChatAsync(chat.Id, gistsToSendByGistIdLastSent[chat.GistIdLastSent])));
    }

    private async Task SendGistsToChatAsync(long chatId, IEnumerable<Gist> gists)
    {
        foreach (var gist in gists)
        {

        }
    }

    private async Task<string> BuildGistMessageAsync(Gist gist)
    {

    }

    private async Task SendMessageAsync(long chatId, string text, ParseMode parseMode = ParseMode.None)
    {
        await _bot!.SendMessage(chatId, text, parseMode, cancellationToken: _serviceCancellationToken!.Value);
        logger?.LogInformation(SentTelegramMessage, "Sent message to chat {ChatId}: {Text}", chatId, text);
    }
}
