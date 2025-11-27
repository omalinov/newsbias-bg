using Microsoft.Data.SqlClient;
using System;

namespace News_Platform
{
    public record ArticleDto(
        int Id,
        int SourceId,
        string Title,
        string? Summary,
        string? Content,
        string? Url,
        DateTime PublishedAt,
        string? Topic,
        string Tone,
        string FactualityLevel
    )
    {
        public static ArticleDto FromReader(SqlDataReader reader)
        {
            return new ArticleDto(
                Id: reader.GetInt32(0),
                SourceId: reader.GetInt32(1),
                Title: reader.GetString(2),
                Summary: reader.IsDBNull(3) ? null : reader.GetString(3),
                Content: reader.IsDBNull(4) ? null : reader.GetString(4),
                Url: reader.IsDBNull(5) ? null : reader.GetString(5),
                PublishedAt: reader.GetDateTime(6),
                Topic: reader.IsDBNull(7) ? null : reader.GetString(7),
                Tone: reader.GetString(8),
                FactualityLevel: reader.GetString(9)
            );
        }
    }

    public static class ArticleConstants
    {
        public static readonly string[] AllowedTones =
        {
            "positive",
            "negative",
            "neutral",
            "sensationalist",
            "unknown"
        };

        public static readonly string[] AllowedFactualityLevels =
        {
            "factual",
            "mixed",
            "opinion",
            "propaganda",
            "tabloid",
            "unknown"
        };
    }

    public record CreateArticleRequest(
        int SourceId,
        string Title,
        string? Summary,
        string? Content,
        string? Url,
        DateTime PublishedAt,
        string? Topic,
        string? Tone,
        string? FactualityLevel
    )
    {
        public string NormalizedTitle
        {
            get
            {
                return Title.Trim();
            }
        }

        public string? NormalizedSummary
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Summary))
                {
                    return null;
                }
                return Summary.Trim();
            }
        }
        public string? NormalizedContent
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Content))
                {
                    return null;
                }
                return Content.Trim();
            }
        }

        public string? NormalizedUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Url))
                {
                    return null;
                }
                return Url.Trim();
            }
        }

        public string? NormalizedTopic
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Topic))
                {
                    return null;
                }
                return Topic.Trim();
            }
        }

        public string NormalizedTone
        {
            get
            {
                return string.IsNullOrWhiteSpace(Tone)
                    ? "unknown"
                    : Tone.Trim().ToLowerInvariant();
            }
        }

        public string NormalizedFactualityLevel
        {
            get
            {
                return string.IsNullOrWhiteSpace(FactualityLevel)
                    ? "unknown"
                    : FactualityLevel.Trim().ToLowerInvariant();
            }
        }

        public bool TryValidate(out string error)
        {
            if (SourceId <= 0)
            {
                error = "SourceId must be a positive integer.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Title))
            {
                error = "Title is required.";
                return false;
            }

            if (!ArticleConstants.AllowedTones.Contains(NormalizedTone))
            {
                error = "Invalid Tone. Allowed values: positive, negative, neutral, sensationalist, unknown.";
                return false;
            }

            if (!ArticleConstants.AllowedFactualityLevels.Contains(NormalizedFactualityLevel))
            {
                error = "Invalid Factuality Level. Allowed values: factual, mixed, opinion, propaganda, tabloid, unknown.";
                return false;
            }

            error = "";
            return true;
        }
        public void FillSqlParameters(SqlCommand command)
        {
            command.Parameters.AddWithValue("@SourceId", SourceId);
            command.Parameters.AddWithValue("@Title", NormalizedTitle);
            command.Parameters.AddWithValue("@Summary", NormalizedSummary ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Content", NormalizedContent ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Url", NormalizedUrl ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@PublishedAt", PublishedAt);
            command.Parameters.AddWithValue("@Topic", NormalizedTopic ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Tone", NormalizedTone);
            command.Parameters.AddWithValue("@FactualityLevel", NormalizedFactualityLevel);
        }
    }
    public static class ArticleEndpoint
    {
        public static void MapArticleEndpoints(this WebApplication app, string connectionString)
        {
            app.MapPost("/articles", (CreateArticleRequest request) => CreateArticleAsync(connectionString, request));
            app.MapGet("/articles", () => ReadAllArticlesAsync(connectionString));
            app.MapGet("/articles/{id:int}", (int id) => ReadSingleArticleAsync(connectionString, id));
            app.MapPut("/articles/{id:int}", (int id, CreateArticleRequest request) => UpdateArticleAsync(connectionString, id, request));
            app.MapDelete("/articles/{id:int}", (int id) => DeleteArticleAsync(connectionString, id));
        }

        private static async Task<IResult> CreateArticleAsync(string connectionString, CreateArticleRequest request)
        {
            if (!request.TryValidate(out var error))
            {
                return Results.BadRequest(new { error });
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            {
                var source = await NewsSourceRepository.GetSourceByIdAsync(connection, request.SourceId);
                if (source is null)
                {
                    return Results.BadRequest(new { error = $"NewsSource with Id={request.SourceId} does not exist." });
                }
            }

            const string sql = @"
                INSERT INTO Article (
                    SourceId,
                    Title,
                    Summary,
                    Content,
                    Url,
                    PublishedAt,
                    Topic,
                    Tone,
                    FactualityLevel
                )
                OUTPUT inserted.Id,
                       inserted.SourceId,
                       inserted.Title,
                       inserted.Summary,
                       inserted.Content,
                       inserted.Url,
                       inserted.PublishedAt,
                       inserted.Topic,
                       inserted.Tone,
                       inserted.FactualityLevel
                VALUES (
                    @SourceId,
                    @Title,
                    @Summary,
                    @Content,
                    @Url,
                    @PublishedAt,
                    @Topic,
                    @Tone,
                    @FactualityLevel
                );";

            await using var command = new SqlCommand(sql, connection);
            request.FillSqlParameters(command);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Results.Problem("Failed to insert Article.");
            }

            var article = ArticleDto.FromReader(reader);
            return Results.Created($"/articles/{article.Id}", article);
        }

        private static async Task<IResult> ReadAllArticlesAsync(string connectionString)
        {
            var articles = new List<ArticleDto>();

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT Id, SourceId, Title, Summary, Content, Url, PublishedAt, Topic, Tone, FactualityLevel
                FROM Article
                ORDER BY Id";

            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var article = ArticleDto.FromReader(reader);
                articles.Add(article);
            }

            return Results.Ok(articles);
        }

        private static async Task<IResult> ReadSingleArticleAsync(string connectionString, int id)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT Id, SourceId, Title, Summary, Content, Url, PublishedAt, Topic, Tone, FactualityLevel
                FROM Article
                WHERE Id = @Id";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", id);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Results.NotFound(new { error = $"Article with Id={id} not found." });
            }

            var article = ArticleDto.FromReader(reader);
            return Results.Ok(article);
        }
        private static async Task<IResult> UpdateArticleAsync(string connectionString, int id, CreateArticleRequest request)
        {
            if (!request.TryValidate(out var error))
            {
                return Results.BadRequest(new { error });
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            {
                var source = await NewsSourceRepository.GetSourceByIdAsync(connection, request.SourceId);
                if (source is null)
                {
                    return Results.BadRequest(new { error = $"NewsSource with Id={request.SourceId} does not exist." });
                }
            }

            const string sql = @"
                    UPDATE Article
                    SET SourceId = @SourceId,
                        Title = @Title,
                        Summary = @Summary,
                        Content = @Content,
                        Url = @Url,
                        PublishedAt = @PublishedAt,
                        Topic = @Topic,
                        Tone = @Tone,
                        FactualityLevel = @FactualityLevel
                    OUTPUT inserted.Id,
                           inserted.SourceId,
                           inserted.Title,
                           inserted.Summary,
                           inserted.Content,
                           inserted.Url,
                           inserted.PublishedAt,
                           inserted.Topic,
                           inserted.Tone,
                           inserted.FactualityLevel
                    WHERE Id = @Id;";

            await using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Id", id);
            request.FillSqlParameters(command);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Results.NotFound(new { error = $"Article with Id={id} not found." });
            }

            var article = ArticleDto.FromReader(reader);
            return Results.Ok(article);
        }
        private static async Task<IResult> DeleteArticleAsync(string connectionString, int id)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                    DELETE FROM Article
                    WHERE Id = @Id;";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", id);

            var affectedRows = await command.ExecuteNonQueryAsync();

            if (affectedRows == 0)
            {
                return Results.NotFound(new { error = $"Article with Id={id} not found." });
            }

            return Results.NoContent();
        }
    }
}