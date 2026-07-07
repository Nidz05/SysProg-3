using Akka.Actor;
using Akka.Event;

namespace TreciProjekat;

// jedan MatchActor prati jednu utakmicu
public class MatchActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly long _fixtureId;

    // interno stanje aktora
    private IReadOnlyList<PlayerInfo> _players = Array.Empty<PlayerInfo>();

    public MatchActor(long fixtureId)
    {
        _fixtureId = fixtureId;

        // azuriranje internog stanja (postave)
        Receive<UpsertMatchPlayers>(msg =>
        {
            _players = msg.Players;
            Logger.Info($"[Akter {_fixtureId}] azurirano stanje: {_players.Count} igraca (nit {Environment.CurrentManagedThreadId})");
        });

        // upit od web servera
        Receive<GetMatchPlayers>(_ =>
        {
            Logger.Info($"[Akter {_fixtureId}] upit -> odgovaram (nit {Environment.CurrentManagedThreadId})");
            if (_players.Count == 0)
                Sender.Tell(new MatchNotAvailable(_fixtureId));
            else
                Sender.Tell(new MatchPlayersResponse(_fixtureId, _players));
        });
    }

    protected override void PreStart() => _log.Info($"MatchActor {_fixtureId} started");
    protected override void PostStop() => _log.Info($"MatchActor {_fixtureId} stopped");

    public static Props Props(long fixtureId) =>
        Akka.Actor.Props.Create(() => new MatchActor(fixtureId));
}
