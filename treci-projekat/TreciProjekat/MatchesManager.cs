using Akka.Actor;
using Akka.Event;

namespace TreciProjekat;

public class MatchesManager : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly Action<long> _track;

    private readonly Dictionary<long, IActorRef> _fixtureIdToActor = new();
    private readonly Dictionary<IActorRef, long> _actorToFixtureId = new();

    public MatchesManager(Action<long> track)
    {
        _track = track;

        // Rx azuriranje (prosledjivanje poruke aktoru)
        Receive<UpsertMatchPlayers>(msg =>
        {
            var actor = GetOrCreate(msg.FixtureId);
            actor.Forward(msg);
        });

        // upit od web servera
        Receive<GetMatchPlayers>(msg =>
        {
            if (_fixtureIdToActor.TryGetValue(msg.FixtureId, out var actor))
            {
                actor.Forward(msg);
            }
            else
            {
                _track(msg.FixtureId);
                Sender.Tell(new MatchNotAvailable(msg.FixtureId));
            }
        });

        // dete aktera je zaustavljeno => uklanjamo ga iz registra
        Receive<Terminated>(t =>
        {
            if (_actorToFixtureId.TryGetValue(t.ActorRef, out var fixtureId))
            {
                _actorToFixtureId.Remove(t.ActorRef);
                _fixtureIdToActor.Remove(fixtureId);
                _log.Info($"MatchActor za utakmicu {fixtureId} je zaustavljen i uklonjen iz registra.");
            }
        });
    }

    private IActorRef GetOrCreate(long fixtureId)
    {
        if (_fixtureIdToActor.TryGetValue(fixtureId, out var existing))
            return existing;

        // matchActor-i => custom dispatcher
        var actor = Context.ActorOf(
            MatchActor.Props(fixtureId).WithDispatcher("match-dispatcher"),
            $"match-{fixtureId}");

        Context.Watch(actor);
        _fixtureIdToActor.Add(fixtureId, actor);
        _actorToFixtureId.Add(actor, fixtureId);
        _log.Info($"Kreiran MatchActor za utakmicu {fixtureId}.");
        return actor;
    }

    protected override void PreStart() => _log.Info("MatchesManager started");
    protected override void PostStop() => _log.Info("MatchesManager stopped");

    protected override SupervisorStrategy SupervisorStrategy() =>
        new OneForOneStrategy(
            maxNrOfRetries: 5,
            withinTimeRange: TimeSpan.FromMinutes(1),
            localOnlyDecider: ex => ex switch
            {
                ArgumentException => Directive.Resume,
                _ => Directive.Restart,
            });

    public static Props Props(Action<long> track) =>
        Akka.Actor.Props.Create(() => new MatchesManager(track));
}
