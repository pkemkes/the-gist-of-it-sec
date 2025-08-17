using GistBackend.Utils;

namespace GistBackend.UnitTest;

public class GistDebouncerTests
{
    [Fact]
    public void IsDebounced_NewGist_False()
    {
        var debouncer = new GistDebouncer();

        var actual = debouncer.IsDebounced(1);

        Assert.False(actual);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void IsDebounced_ExponentialBounceCount_False(int bounceCount)
    {
        const int testGistId = 1;
        var debouncer = new GistDebouncer();
        var results = Enumerable.Range(0, bounceCount - 1).ToList().Select(_ => debouncer.IsDebounced(testGistId));

        var actual = debouncer.IsDebounced(testGistId);

        Assert.False(actual);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(30)]
    [InlineData(55)]
    public void IsDebounced_NotExponentialBounceCount_True(int bounceCount)
    {
        const int testGistId = 1;
        var debouncer = new GistDebouncer();
        Enumerable.Range(0, bounceCount - 1).ToList().ForEach(_ => debouncer.IsDebounced(testGistId));

        var actual = debouncer.IsDebounced(testGistId);

        Assert.True(actual);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(299)]
    public void ResetDebouncedState_BouncedBefore_StateReset(int bounceCount)
    {
        const int testGistId = 1;
        var debouncer = new GistDebouncer();
        Enumerable.Range(0, bounceCount).ToList().ForEach(_ => debouncer.IsDebounced(testGistId));

        debouncer.ResetDebounceState(testGistId);

        var actual = debouncer.IsDebounced(testGistId);
        Assert.False(actual);
    }

    [Fact]
    public void GetDebouncedCount_SomeGistsAreDebouncedNextTime_NumberOfThoseGists()
    {
        var notDebouncedGistIds = new List<int> { 3, 4, 5 };
        var debouncedGistIds = new List<int> { 6, 7 };
        var debouncer = new GistDebouncer();
        notDebouncedGistIds.ForEach(gistId =>
            Enumerable.Range(0, (int)Math.Pow(2, gistId) - 1).ToList().ForEach(_ => debouncer.IsDebounced(gistId)));
        debouncedGistIds.ForEach(gistId =>
            Enumerable.Range(0, (int)Math.Pow(2, gistId)).ToList().ForEach(_ => debouncer.IsDebounced(gistId)));

        var actual = debouncer.GetDebouncedGistsCount();

        Assert.Equal(debouncedGistIds.Count, actual);
    }
}
