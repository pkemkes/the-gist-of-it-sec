using GistBackend.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static Telegram.Bot.TelegramBotClient;

namespace GistBackend.Handlers.TelegramBotClientHandler;

public interface ITelegramBotClientHandler
{
    public void StartBotClient(IEnumerable<BotCommand> commands, OnMessageHandler onMessage, OnErrorHandler onError,
        CancellationToken ct);
    public Task SendMessageAsync(long chatId, string text, ParseMode parseMode = ParseMode.None);
}

public class TelegramBotClientHandler(
    IOptions<TelegramBotClientHandlerOptions> options,
    ILogger<TelegramBotClient>? logger) : ITelegramBotClientHandler
{
    private TelegramBotClient? _botClient;
    private CancellationToken? _cancellationToken;

    public void StartBotClient(IEnumerable<BotCommand> commands, OnMessageHandler onMessage, OnErrorHandler onError,
        CancellationToken ct)
    {
        _cancellationToken = ct;
        if (string.IsNullOrWhiteSpace(options.Value.BotToken))
            throw new ArgumentException("Bot token is not set in the options.");
        _botClient = new TelegramBotClient(options.Value.BotToken, cancellationToken: ct);
        _botClient.SetMyCommands(commands, cancellationToken: ct).GetAwaiter().GetResult();
        _botClient.OnMessage += onMessage;
        _botClient.OnError += onError;
    }

    public async Task SendMessageAsync(long chatId, string text, ParseMode parseMode = ParseMode.None)
    {
        if (_botClient is null) throw new InvalidOperationException("Bot client is not initialized.");

        await _botClient.SendMessage(chatId, text, parseMode, cancellationToken: _cancellationToken!.Value);
        logger?.LogInformation(LogEvents.SentTelegramMessage, "Sent message to chat {ChatId}: {Text}", chatId, text);
    }
}
