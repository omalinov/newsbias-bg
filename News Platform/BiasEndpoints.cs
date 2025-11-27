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
    public static class BiasEndpoints
    {
        public static void MapBiasEndpoints(this WebApplication app, string connectionString)
        {
            app.MapGet("/articles/bias-summary", () => GetArticlesBiasSummaryAsync(connectionString));
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
    }
}
