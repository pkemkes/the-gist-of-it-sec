namespace GistBackend.IntegrationTest.Utils;

public static class RandomExtensions {
    private const string Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 -.,!?%/()§&";

    public static string NextString(this Random random, int length = 50) => new(
        Enumerable.Repeat(Characters, length).Select(s => s[random.Next(s.Length)]).ToArray()
    );

    public static string[] NextArrayOfStrings(this Random random, int? length = null) => Enumerable
        .Repeat("", length ?? random.Next(1, 11)).Select(_ => random.NextString()).ToArray();

    public static DateTime NextDateTime(this Random random, DateTime? min = null, DateTime? max = null)
    {
        min ??= DateTime.UnixEpoch;
        max ??= DateTime.UnixEpoch.AddYears(100);
        var maxSeconds = (max - min).Value.Seconds;
        return min.Value.AddSeconds(random.Next(maxSeconds));
    }
}
