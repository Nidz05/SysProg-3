namespace TreciProjekat;

public static class Config
{
    public static string SportMonksToken { get; } = "r7kR5eIHSgqQA1aAy3iLJUlgYbpxZkv0HHqLZ3oULlL9z0N5lQKYl24SzPHC";

    public static long[] TrackedFixtureIds { get; } = { 18535517, 18531147 };

    // period Rx osvezavanja postava
    public static TimeSpan PollInterval { get; } = TimeSpan.FromSeconds(30);

    // vremenski limit za Ask upit web servera ka akterima
    public static TimeSpan AskTimeout { get; } = TimeSpan.FromSeconds(5);

    public const string ServerPrefix = "http://localhost:8080/";

    public const string SportMonksFixtureUrl = "https://api.sportmonks.com/v3/football/fixtures/";
    public const string LineupsInclude = "lineups.player.country";
}
