using Akka.Actor;
using Akka.Configuration;

namespace TreciProjekat;

public static class Program
{
    public static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== Treci projekat: SportMonks + Rx.NET + Akka.NET ===\n");

        if (Config.SportMonksToken == "YOUR_SPORTMONKS_TOKEN")
            Logger.Error("SportMonks token nije podesen (Config.cs ili env var SPORTMONKS_TOKEN) -> Rx nece imati podatke.");

        var config = ConfigurationFactory.ParseString(@"
            akka {
                stdout-loglevel = OFF
                loglevel = INFO
            }
            match-dispatcher {
                type = Dispatcher
                throughput = 100
                dedicated-thread-pool {
                    thread-count = 4
                }
            }
        ");

        var system = ActorSystem.Create("match-system", config);

        var client = new SportMonksClient();
        var feed = new FixtureFeed(client);

        var manager = system.ActorOf(MatchesManager.Props(feed.Track), "manager");

        feed.Start(manager);

        var server = new WebServer(manager);
        server.Start();

        Console.WriteLine();
        Logger.Info("Pritisni Enter za gasenje...");
        Console.ReadLine();

        feed.Dispose();
        server.Stop();
        await system.Terminate();
        Logger.Info("Aplikacija zatvorena.");
    }
}
