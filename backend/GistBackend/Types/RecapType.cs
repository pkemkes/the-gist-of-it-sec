namespace GistBackend.Types;

public enum RecapType
{
    Daily,
    Weekly
}

public static class RecapTypeExtensions
{
    public static string ToTypeString(this RecapType recapType)
    {
        return recapType switch
        {
            RecapType.Daily => "Daily",
            RecapType.Weekly => "Weekly",
            _ => throw new ArgumentOutOfRangeException(nameof(recapType), recapType, "Invalid recap type")
        };
    }
}
