using GistBackend.Types;

namespace TestUtilities;

public static class RandomExtensions {
    private const string Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    extension(Random random)
    {
        public string NextString(int length = 50) => new(
            Enumerable.Repeat(Characters, length).Select(s => s[random.Next(s.Length)]).ToArray()
        );

        public Uri NextUri(int partLength = 50) =>
            new($"https://www.{random.NextString(partLength)}.com/{random.NextString(partLength)}");

        public string[] NextArrayOfStrings(int? length = null) => Enumerable
            .Repeat("", length ?? random.Next(1, 11)).Select(_ => random.NextString()).ToArray();

        public DateTime NextDateTime(DateTime? min = null, DateTime? max = null)
        {
            min ??= DateTime.UnixEpoch;
            max ??= DateTime.UnixEpoch.AddYears(100);
            var maxSeconds = (int)(max - min).Value.TotalSeconds;
            return min.Value.AddSeconds(random.Next(maxSeconds));
        }

        public FeedType NextFeedType() =>
            (FeedType)random.Next(Enum.GetValues<FeedType>().Length);
    }
}
