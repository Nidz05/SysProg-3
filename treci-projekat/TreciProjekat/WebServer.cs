using System.Net;
using System.Text;
using System.Text.Json;
using Akka.Actor;

namespace TreciProjekat;

public class WebServer
{
    private readonly IActorRef _manager;
    private readonly HttpListener _listener = new();
    private Thread? _acceptThread;
    private volatile bool _running;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public WebServer(IActorRef manager)
    {
        _manager = manager;
        _listener.Prefixes.Add(Config.ServerPrefix);
    }

    public void Start()
    {
        _listener.Start();
        _running = true;
        _acceptThread = new Thread(AcceptLoop) { Name = "web-accept", IsBackground = false };
        _acceptThread.Start();
        Logger.Info($"Web server slusa na {Config.ServerPrefix} (primer: {Config.ServerPrefix}match?id=18535517).");
    }

    private void AcceptLoop()
    {
        while (_running)
        {
            HttpListenerContext context;
            try { context = _listener.GetContext(); }
            catch (HttpListenerException) { break; }
            catch (InvalidOperationException) { break; }
            _ = HandleAsync(context);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var req = context.Request;
        string path = req.Url?.AbsolutePath.ToLowerInvariant() ?? "/";
        Logger.Request($"{req.HttpMethod} {req.Url}");

        try
        {
            if (path == "/")
            {
                await SendText(context, HelpText, 200);
                return;
            }

            if (path == "/match")
            {
                string? idText = req.QueryString["id"];
                if (!long.TryParse(idText, out long fixtureId))
                {
                    await SendText(context, "Nedostaje ili je neispravan parametar 'id'. Primer: /match?id=18535517", 400);
                    return;
                }

                // komunikacija server - akteri
                var result = await _manager.Ask<IMatchQueryResult>(new GetMatchPlayers(fixtureId), Config.AskTimeout);

                switch (result)
                {
                    case MatchPlayersResponse ok:
                        await SendJson(context, JsonSerializer.Serialize(ok.Players, JsonOptions), 200);
                        Logger.Response($"Utakmica {fixtureId}: vraceno {ok.Players.Count} igraca.");
                        break;

                    case MatchNotAvailable:
                        await SendJson(context,
                            $"{{ \"poruka\": \"Nema podataka za utakmicu {fixtureId} (jos nije preuzeta ili se ne prati).\" }}",
                            404);
                        Logger.Response($"Utakmica {fixtureId}: nema podataka.");
                        break;
                }
                return;
            }

            await SendText(context, "Nepoznata putanja. Probaj: /match?id=18535517", 404);
        }
        catch (Exception ex)
        {
            Logger.Error($"Greska pri obradi zahteva: {ex.Message}");
            try { await SendText(context, $"Interna greska: {ex.Message}", 500); }
            catch { /* stream je mozda vec zatvoren */ }
        }
    }

    private static Task SendText(HttpListenerContext ctx, string text, int status)
        => Send(ctx, Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", status);

    private static Task SendJson(HttpListenerContext ctx, string json, int status)
        => Send(ctx, Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", status);

    private static async Task Send(HttpListenerContext ctx, byte[] body, string contentType, int status)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength64 = body.Length;
        await ctx.Response.OutputStream.WriteAsync(body);
        ctx.Response.OutputStream.Close();
    }

    public void Stop()
    {
        _running = false;
        try { _listener.Stop(); } catch { }
        _acceptThread?.Join();
        try { _listener.Close(); } catch { }
        Logger.Info("Web server zaustavljen.");
    }

    private const string HelpText =
        "Treci projekat - SportMonks + Rx.NET + Akka.NET\n" +
        "Zahtev: GET /match?id=<fixture_id>\n" +
        "Primer: /match?id=18535517\n" +
        "Server vraca trenutno stanje (igrace u postavi) koje akteri odrzavaju,\n" +
        "a koje Rx periodicno azurira iz SportMonks API-ja.";
}
