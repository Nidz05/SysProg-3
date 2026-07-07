using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace KlijentZaTestiranje;

class Program
{
    private static readonly HttpClient client = new();
    private static readonly object _lock = new();
    private const string baseUrl = "http://localhost:8080/match?id=";

    private const long pracenaA = 18535517;
    private const long pracenaB = 18531147;
    
    private const long nepracenaAliPostoji = 18535518;

    private const long nepostojeca = 99999999;

    private static readonly Dictionary<string, long[]> testCases = new()
    {
        ["1"] = new[] { pracenaA },                                 // jedan zahtev za pracenu utakmicu
        ["2"] = new[] { pracenaA, pracenaB, nepracenaAliPostoji },  // vise razlicitih (+ nepracenaAliPostoji )
        ["3"] = Enumerable.Repeat(pracenaA, 20).ToArray(),          // 20 istovremenih zahteva za istu utakmicu
        ["4"] = new[] { nepostojeca },                              // utakmica koja ne postoji (ocekivano 404)
    };

    private static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== Klijent za testiranje (treci projekat) ===");
        Console.WriteLine("Server treba da radi na http://localhost:8080/");
        Console.WriteLine("Unesi kod scenarija (prazan Enter za izlaz):");
        Console.WriteLine("  \"1\" - jedan zahtev za pracenu utakmicu");
        Console.WriteLine("  \"2\" - vise razlicitih utakmica (ukljucujuci nepracenu, ali postojecu)");
        Console.WriteLine("  \"3\" - 20 ISTOVREMENIH zahteva za istu utakmicu (konkurentno citanje)");
        Console.WriteLine("  \"4\" - utakmica koja ne postoji (ocekivano 404)");

        while (true)
        {
            Console.Write("\n> ");
            string input = Console.ReadLine() ?? "";
            if (input == "") break;

            if (!testCases.TryGetValue(input, out long[]? ids))
            {
                Console.WriteLine($"Nepoznat kod: \"{input}\"");
                continue;
            }

            bool showBody = ids.Length <= 3;

            List<Task<bool>> tasks = ids.Select(id => SendAsync(id, showBody)).ToList();
            bool[] results = await Task.WhenAll(tasks);

            int ok = results.Count(r => r);
            Console.WriteLine($"Uspesnih (2xx): {ok}/{results.Length}, ostalih: {results.Length - ok}/{results.Length}");
            Console.WriteLine("------------------------------------------------");
        }
    }

    private static async Task<bool> SendAsync(long fixtureId, bool showBody)
    {
        string url = baseUrl + fixtureId;
        try
        {
            HttpResponseMessage resp = await client.GetAsync(url);
            string body = await resp.Content.ReadAsStringAsync();
            lock (_lock)
            {
                Console.WriteLine($"[{(int)resp.StatusCode} {resp.StatusCode}] utakmica {fixtureId}");
                if (showBody)
                    Console.WriteLine(Skrati(body));
            }
            return resp.IsSuccessStatusCode;
        }
        catch (Exception e)
        {
            lock (_lock)
            {
                Console.WriteLine($"Greska za utakmicu {fixtureId}: {e.Message}");
            }
            return false;
        }
    }

    private static string Skrati(string text, int max = 800)
        => text.Length <= max ? text : text.Substring(0, max) + " ... [skraceno]";
}
