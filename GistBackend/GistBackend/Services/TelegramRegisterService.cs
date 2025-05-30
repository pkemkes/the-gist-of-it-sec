using GistBackend.Handler.MariaDbHandler;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static GistBackend.Utils.LogEvents;
using static GistBackend.Utils.ServiceUtils;

namespace GistBackend.Services;

public class TelegramRegisterService(
    IMariaDbHandler mariaDbHandler,
    IOptions<TelegramServiceOptions> options,
    ILogger<TelegramRegisterService>? logger = null
) : BackgroundService
{
    private CancellationToken? _serviceCancellationToken;
    private TelegramBotClient? _bot;
    private static readonly BotCommand StartCommand = new("start", "Register to receive messages");
    private static readonly BotCommand StopCommand = new("stop", "Unregister to stop receiving messages");
    private static readonly List<BotCommand> Commands = [StartCommand, StopCommand];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _serviceCancellationToken = ct;
        _bot = new TelegramBotClient(options.Value.BotToken, cancellationToken: ct);
        await _bot.SetMyCommands([StartCommand, StopCommand], cancellationToken: ct);
        _bot.OnMessage += OnMessageAsync;
        while (!ct.IsCancellationRequested)
        {
            var startTime = DateTime.UtcNow;
            await DelayUntilNextExecutionAsync(startTime, 0.1, null, ct);
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
            // Handle non-command messages if needed
            await SendMessageAsync(message.Chat.Id, "Please use a command to interact with the bot.");
        }
    }

    private async Task HandleCommandAsync(Message message)
    {
        var command = message.Text![1..];
        if (command == StartCommand.Command) await HandleStartCommandAsync(message);
        else if (command == StopCommand.Command) await HandleStopCommandAsync(message);
        else
        {
            var availableCommands = string.Join(", ", Commands.Select(c => $"/{c.Command}"));
            await SendMessageAsync(message.Chat.Id,
                $"Unknown command. Please use one of the following commands: {availableCommands}");
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
        // Logic to unregister the user from receiving messages
        // This could involve removing the user ID from a database or marking them as inactive
        await SendMessageAsync(message.Chat.Id, "You have been unregistered and will no longer receive messages.");
    }

    private async Task SendMessageAsync(long chatId, string text) =>
        await _bot!.SendMessage(chatId, text, cancellationToken: _serviceCancellationToken!.Value);
}
