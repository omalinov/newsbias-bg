using Microsoft.Data.SqlClient;

namespace News_Platform
{
    public record UserFeedArticleDto(
        int Id,
        string Title,
        string? Summary,
        string? Url,
        DateTime PublishedAt,
        string? Topic,
        string Tone,
        string FactualityLevel,
        int SourceId,
        string SourceName,
        string PoliticalLeaning
    )
    {
        public static UserFeedArticleDto FromReader(SqlDataReader reader)
        {
            return new UserFeedArticleDto(
                Id: reader.GetInt32(0),
                Title: reader.GetString(1),
                Summary: reader.IsDBNull(2) ? null : reader.GetString(2),
                Url: reader.IsDBNull(3) ? null : reader.GetString(3),
                PublishedAt: reader.GetDateTime(4),
                Topic: reader.IsDBNull(5) ? null : reader.GetString(5),
                Tone: reader.GetString(6),
                FactualityLevel: reader.GetString(7),
                SourceId: reader.GetInt32(8),
                SourceName: reader.GetString(9),
                PoliticalLeaning: reader.GetString(10)
            );
        }
    }

    public static class UserFeedEndpoints
    {
        public static void MapUserFeedEndpoints(this WebApplication app, string connectionString)
        {
            app.MapGet("/users/{id:int}/feed", (int id) => GetUserFeedAsync(connectionString, id));
        }

        private static async Task<IResult> GetUserFeedAsync(string connectionString, int userId)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            {
                var user = await AppUserRepository.GetByIdAsync(connection, userId);
                if (user is null)
                {
                    return Results.NotFound(new { error = $"AppUser with Id={userId} not found." });
                }
            }

            const string sql = @"
                SELECT
                    a.Id,
                    a.Title,
                    a.Summary,
                    a.Url,
                    a.PublishedAt,
                    a.Topic,
                    a.Tone,
                    a.FactualityLevel,
                    ns.Id   AS SourceId,
                    ns.Name AS SourceName,
                    ns.PoliticalLeaning
                FROM AppUser au
                INNER JOIN UserPreference up ON up.UserId = au.Id
                INNER JOIN Article a         ON a.Topic = up.Topic
                INNER JOIN NewsSource ns     ON ns.Id = a.SourceId
                WHERE au.Id = @UserId
                ORDER BY a.PublishedAt DESC;";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);

            var feed = new List<UserFeedArticleDto>();

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var article = UserFeedArticleDto.FromReader(reader);
                feed.Add(article);
            }

            return Results.Ok(feed);
        }
    }
}
