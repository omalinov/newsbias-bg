using Microsoft.Data.SqlClient;

namespace News_Platform
{
    public record BiasByToneDto(
        string Tone,
        int Count
    );

    public record BiasByLeaningDto(
        string PoliticalLeaning,
        int TotalArticles,
        IReadOnlyList<BiasByToneDto> ByTone
    );

    public record ArticlesBiasSummaryDto(
        int TotalArticles,
        IReadOnlyList<BiasByLeaningDto> ByLeaning
    );

    public record SourceToneCountDto(
        string Tone,
        int Count
    );

    public record SourceBiasDto(
        int SourceId,
        string Name,
        string PoliticalLeaning,
        int TotalArticles,
        IReadOnlyList<SourceToneCountDto> ByTone
    );
    public static class BiasEndpoints
    {
        public static void MapBiasEndpoints(this WebApplication app, string connectionString)
        {
            app.MapGet("/articles/bias-summary", () => GetArticlesBiasSummaryAsync(connectionString));
            app.MapGet("/articles/bias-by-source", () => GetBiasBySourceAsync(connectionString));
        }

        private static async Task<IResult> GetArticlesBiasSummaryAsync(string connectionString)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT ns.PoliticalLeaning,
                       a.Tone,
                       COUNT(*) AS ArticleCount
                FROM Article a
                INNER JOIN NewsSource ns ON ns.Id = a.SourceId
                GROUP BY ns.PoliticalLeaning, a.Tone
                ORDER BY ns.PoliticalLeaning, a.Tone;";

            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            var byLeaningDict = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            var totalArticles = 0;

            while (await reader.ReadAsync())
            {
                var politicalLeaning = reader.GetString(0);
                var tone = reader.GetString(1);
                var count = reader.GetInt32(2);

                totalArticles += count;

                if (!byLeaningDict.TryGetValue(politicalLeaning, out var toneDict))
                {
                    toneDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    byLeaningDict[politicalLeaning] = toneDict;
                }

                toneDict[tone] = count;
            }

            var byLeaning = new List<BiasByLeaningDto>();

            foreach (var (leaning, toneDict) in byLeaningDict)
            {
                var tones = toneDict
                    .Select(kvp => new BiasByToneDto(kvp.Key, kvp.Value))
                    .ToList();

                var totalForLeaning = toneDict.Values.Sum();

                byLeaning.Add(new BiasByLeaningDto(
                    PoliticalLeaning: leaning,
                    TotalArticles: totalForLeaning,
                    ByTone: tones
                ));
            }

            var summary = new ArticlesBiasSummaryDto(
                TotalArticles: totalArticles,
                ByLeaning: byLeaning
            );

            return Results.Ok(summary);
        }

        private static async Task<IResult> GetBiasBySourceAsync(string connectionString)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    ns.Id AS SourceId,
                    ns.Name,
                    ns.PoliticalLeaning,
                    a.Tone,
                    COUNT(*) AS ArticleCount
                FROM Article a
                INNER JOIN NewsSource ns ON ns.Id = a.SourceId
                GROUP BY ns.Id, ns.Name, ns.PoliticalLeaning, a.Tone
                ORDER BY ns.Name, a.Tone;";

            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            var bySource = new Dictionary<int, (string Name, string PoliticalLeaning, Dictionary<string, int> ToneCounts)>();

            while (await reader.ReadAsync())
            {
                var sourceId = reader.GetInt32(0);
                var name = reader.GetString(1);
                var politicalLeaning = reader.GetString(2);
                var tone = reader.GetString(3);
                var count = reader.GetInt32(4);

                if (!bySource.TryGetValue(sourceId, out var entry))
                {
                    entry = (name, politicalLeaning, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
                    bySource[sourceId] = entry;
                }

                entry.ToneCounts[tone] = count;
            }

            var result = new List<SourceBiasDto>();

            foreach (var (sourceId, entry) in bySource)
            {
                var toneDtos = entry.ToneCounts
                    .Select(kvp => new SourceToneCountDto(kvp.Key, kvp.Value))
                    .ToList();

                var totalArticles = entry.ToneCounts.Values.Sum();

                result.Add(new SourceBiasDto(
                    SourceId: sourceId,
                    Name: entry.Name,
                    PoliticalLeaning: entry.PoliticalLeaning,
                    TotalArticles: totalArticles,
                    ByTone: toneDtos
                ));
            }

            return Results.Ok(result);
        }
    }
}
