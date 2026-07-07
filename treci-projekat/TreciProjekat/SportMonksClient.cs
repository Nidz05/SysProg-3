using System.Text.Json;
using System.Text.RegularExpressions;

namespace TreciProjekat;

public class SportMonksClient
{
    private readonly HttpClient _http = new();

    public async Task<string> FetchFixtureRawAsync(long fixtureId, CancellationToken ct)
    {
        string url = $"{Config.SportMonksFixtureUrl}{fixtureId}?include={Config.LineupsInclude}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", Config.SportMonksToken);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}

public static class SportMonksMapper
{
    public static MatchSnapshot Map(long fixtureId, string rawJson)
    {
        var players = new List<PlayerInfo>();

        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return new MatchSnapshot(fixtureId, players);

        if (!data.TryGetProperty("lineups", out var lineups) || lineups.ValueKind != JsonValueKind.Array)
            return new MatchSnapshot(fixtureId, players);

        foreach (var entry in lineups.EnumerateArray())
        {
            int? brojDresa = entry.TryGetProperty("jersey_number", out var j) && j.ValueKind == JsonValueKind.Number
                ? j.GetInt32()
                : (int?)null;

            string ime = "", prezime = "", zemlja = "";
            int? godina = null;

            if (entry.TryGetProperty("player", out var player) && player.ValueKind == JsonValueKind.Object)
            {
                ime = GetString(player, "firstname");
                prezime = GetString(player, "lastname");
                godina = ExtractYear(GetString(player, "date_of_birth"));
                if (player.TryGetProperty("country", out var country) && country.ValueKind == JsonValueKind.Object)
                    zemlja = GetString(country, "name");
            }

            // ako nema ugnjezdenog 'player', koristimo player_name sa stavke postave
            if (ime.Length == 0 && prezime.Length == 0)
                (ime, prezime) = SplitName(GetString(entry, "player_name"));

            // odbaci praznu/nepostojecu stavku postave (nema ni imena ni broja dresa)
            if (ime.Length == 0 && prezime.Length == 0 && brojDresa is null)
                continue;

            players.Add(new PlayerInfo(ime, prezime, godina, brojDresa, zemlja));
        }

        return new MatchSnapshot(fixtureId, players);
    }

    private static string GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    // ocekivani format datuma je "yyyy-mm-dd"
    private static int? ExtractYear(string date)
    {
        if (string.IsNullOrWhiteSpace(date)) return null;
        var m = Regex.Match(date, @"\d{4}");
        return m.Success ? int.Parse(m.Value) : (int?)null;
    }

    private static (string ime, string prezime) SplitName(string full)
    {
        if (string.IsNullOrWhiteSpace(full)) return ("", "");
        var parts = full.Trim().Split(' ', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], "");
    }
}
