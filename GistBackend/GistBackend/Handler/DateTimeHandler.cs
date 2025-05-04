namespace GistBackend.Handler;

public interface IDateTimeHandler
{
    DateTime GetUtcNow();
}

public class DateTimeHandler : IDateTimeHandler
{
    public DateTime GetUtcNow() => DateTime.UtcNow;
}
