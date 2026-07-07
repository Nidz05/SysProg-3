using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Akka.Actor;

namespace TreciProjekat;

public class FixtureFeed : IDisposable
{
    private readonly SportMonksClient _client;
    private IActorRef _manager = ActorRefs.Nobody;
    private IDisposable? _subscription;

    private readonly ReplaySubject<long> _fixturesToTrack = new();

    public FixtureFeed(SportMonksClient client)
    {
        _client = client;
    }

    public void Start(IActorRef manager)
    {
        _manager = manager;

        _subscription = _fixturesToTrack
            .Distinct()                    
            .SelectMany(BuildStreamFor)  
            // Rx je po defaultu single-threaded, konkurentnost ostvarujemo Scheduler-ima
            .SubscribeOn(TaskPoolScheduler.Default)
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(
                onNext: snapshot =>
                {
                    // emitovanje ka akterima
                    _manager.Tell(new UpsertMatchPlayers(snapshot.FixtureId, snapshot.Players));
                    Logger.Info($"[Rx] Utakmica {snapshot.FixtureId}: prosledjeno {snapshot.Players.Count} igraca akterima (nit {Environment.CurrentManagedThreadId}).");
                },
                onError: ex => Logger.Error($"[Rx] Neocekivana greska u toku: {ex.Message}"));

        // pocetni skup pracenja iz konfiguracije
        foreach (var id in Config.TrackedFixtureIds)
            _fixturesToTrack.OnNext(id);

        Logger.Info($"[Rx] Pokrenut tok; pocetno pracenih utakmica: {Config.TrackedFixtureIds.Length} (period {Config.PollInterval.TotalSeconds}s).");
    }

    // menadzer ovo poziva kad stigne zahtev za utakmicu koju jos ne pratimo
    public void Track(long fixtureId)
    {
        Logger.Info($"[Rx] Dodajem utakmicu {fixtureId} u pracenje (na zahtev korisnika).");
        _fixturesToTrack.OnNext(fixtureId);
    }

    private IObservable<MatchSnapshot> BuildStreamFor(long fixtureId)
    {
        return Observable
            .Timer(TimeSpan.Zero, Config.PollInterval)
            .SelectMany(_ => Observable.FromAsync(ct => TryFetchRawAsync(fixtureId, ct)))
            .Where(raw => !string.IsNullOrWhiteSpace(raw))          // odbaci prazan/nepostojeci odgovor
            .Select(raw => SportMonksMapper.Map(fixtureId, raw!))   
            .Where(snapshot => snapshot.Players.Count > 0);         // odbaci ako nema (validne) postave
    }

    private async Task<string?> TryFetchRawAsync(long fixtureId, CancellationToken ct)
    {
        try
        {
            return await _client.FetchFixtureRawAsync(fixtureId, ct);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error($"[Rx] Neuspeo poziv za utakmicu {fixtureId}: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _fixturesToTrack.Dispose();
    }
}
