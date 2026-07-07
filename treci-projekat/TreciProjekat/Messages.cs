namespace TreciProjekat;

// azuriranje internog stanja utakmice novom postavom
public record UpsertMatchPlayers(long FixtureId, IReadOnlyList<PlayerInfo> Players);

// upit za trenutno stanje utakmice
public record GetMatchPlayers(long FixtureId);

public interface IMatchQueryResult
{
    long FixtureId { get; }
}

public record MatchPlayersResponse(long FixtureId, IReadOnlyList<PlayerInfo> Players) : IMatchQueryResult;

public record MatchNotAvailable(long FixtureId) : IMatchQueryResult;
