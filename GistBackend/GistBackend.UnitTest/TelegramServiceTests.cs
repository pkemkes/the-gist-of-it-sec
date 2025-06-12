using GistBackend.Handlers.TelegramBotClientHandler;
using GistBackend.Services;

namespace GistBackend.UnitTest;

public class TelegramServiceTests
{
    private class TestableTelegramService : TelegramService
    {
        public TestableTelegramService(TelegramServiceOptions options, ITelegramBotClientHandler botClientHandler)
            : base(options, botClientHandler)
        {
        }

        public void SetChatLastSentGistId(Chat chat, int gistId)
        {
            chat.GistIdLastSent = gistId;
        }
    }
}
