namespace GistBackend.Utils;

public static class DateTimeExtensions
{
    public static string ToDatabaseCompatibleString(this DateTime dateTime) =>
        dateTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ"); // 6 decimal places to match database
}
