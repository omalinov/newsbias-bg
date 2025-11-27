using Microsoft.Data.SqlClient;

namespace News_Platform
{
    public record NewsSourceDto(
        int Id,
        string Name,
        string Url,
        string? Category,
        bool IsActive,
        string PoliticalLeaning,
        DateTime CreatedAt
    )
    {
        public static NewsSourceDto FromReader(SqlDataReader reader)
        {
            return new NewsSourceDto(
                Id: reader.GetInt32(0),
                Name: reader.GetString(1),
                Url: reader.GetString(2),
                Category: reader.IsDBNull(3) ? null : reader.GetString(3),
                IsActive: reader.GetBoolean(4),
                PoliticalLeaning: reader.GetString(5),
                CreatedAt: reader.GetDateTime(6)
            );
        }
    }

    public static class NewsSourceConstants
    {
        public static readonly string[] AllowedPoliticalLeanings =
        {
            "pro-eu",
            "pro-russia",
            "neutral",
            "unknown"
        };
    }

    public record CreateNewsSourceRequest(
        string Name,
        string Url,
        string? Category,
        bool? IsActive,
        string? PoliticalLeaning
    )
    {
        public string NormalizedName
        {
            get
            {
                return Name.Trim();
            }
        }

        public string NormalizedUrl
        {
            get
            {
                return Url.Trim();
            }
        }
        public string? NormalizedCategory
        {
            get
            {
                if (Category is null)
                {
                    return null;
                }
                return Category.Trim();
            }
        }

        public bool NormalizedIsActive
        {
            get
            {
                return IsActive ?? true;
            }
        }

        public string NormalizedPoliticalLeaning
        {
            get
            {
                return string.IsNullOrWhiteSpace(PoliticalLeaning)
                    ? "unknown"
                    : PoliticalLeaning.Trim().ToLowerInvariant();
            }
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                error = "Name is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Url))
            {
                error = "Url is required.";
                return false;
            }

            if (!NewsSourceConstants.AllowedPoliticalLeanings.Contains(NormalizedPoliticalLeaning))
            {
                error = "Invalid PoliticalLeaning. Allowed values: pro-EU, pro-Russia, neutral, unknown.";
                return false;
            }

            error = "";
            return true;
        }
        public void FillSqlParameters(SqlCommand command)
        {
            command.Parameters.AddWithValue("@Name", NormalizedName);
            command.Parameters.AddWithValue("@Url", NormalizedUrl);
            command.Parameters.AddWithValue("@Category", NormalizedCategory ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@IsActive", NormalizedIsActive);
            command.Parameters.AddWithValue("@PoliticalLeaning", NormalizedPoliticalLeaning);
        }
    }

    public static class SourceEndpoints
    {
        public static void MapSourceEndpoints(this WebApplication app, string connectionString)
        {
            app.MapPost("/sources", (CreateNewsSourceRequest request) => CreateSourceAsync(connectionString, request));
            app.MapGet("/sources", () => ReadAllSourcesAsync(connectionString));
            app.MapGet("/sources/{id:int}", (int id) => ReadSingleSourceAsync(connectionString, id));
            app.MapPut("/sources/{id:int}", (int id, CreateNewsSourceRequest request) => UpdateSourceAsync(connectionString, id, request));
            app.MapDelete("/sources/{id:int}", (int id) => DeleteSourceAsync(connectionString, id));
        }

        private static async Task<IResult> CreateSourceAsync(string connectionString, CreateNewsSourceRequest request)
        {
            if (!request.TryValidate(out var error))
            {
                return Results.BadRequest(new { error });
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                INSERT INTO NewsSource (Name, Url, Category, IsActive, PoliticalLeaning)
                OUTPUT inserted.Id,
                       inserted.Name,
                       inserted.Url,
                       inserted.Category,
                       inserted.IsActive,
                       inserted.PoliticalLeaning,
                       inserted.CreatedAt
                VALUES (@Name, @Url, @Category, @IsActive, @PoliticalLeaning);";

            await using var command = new SqlCommand(sql, connection);

            request.FillSqlParameters(command);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Results.Problem("Failed to insert NewsSource.");
            }
            var source = NewsSourceDto.FromReader(reader);
            return Results.Created($"/sources/{source.Id}", source);
        }

        private static async Task<IResult> ReadAllSourcesAsync(string connectionString)
        {
            var sources = new List<NewsSourceDto>();

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT Id, Name, Url, Category, IsActive, PoliticalLeaning, CreatedAt
                FROM NewsSource
                ORDER BY Id";

            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var source = NewsSourceDto.FromReader(reader);
                sources.Add(source);
            }

            return Results.Ok(sources);
        }

        private static async Task<IResult> ReadSingleSourceAsync(string connectionString, int id)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var source = await NewsSourceRepository.GetSourceByIdAsync(connection, id);
            if (source is null)
            {
                return Results.NotFound(new { error = $"NewsSource with Id={id} not found." });
            }
            return Results.Ok(source);
        }

        private static async Task<IResult> UpdateSourceAsync(string connectionString, int id, CreateNewsSourceRequest request)
        {
            if (!request.TryValidate(out var error))
            {
                return Results.BadRequest(new { error });
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                    UPDATE NewsSource
                    SET Name = @Name,
                        Url = @Url,
                        Category = @Category,
                        IsActive = @IsActive,
                        PoliticalLeaning = @PoliticalLeaning
                    OUTPUT inserted.Id,
                           inserted.Name,
                           inserted.Url,
                           inserted.Category,
                           inserted.IsActive,
                           inserted.PoliticalLeaning,
                           inserted.CreatedAt
                    WHERE Id = @Id;";

            await using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Id", id);
            request.FillSqlParameters(command);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Results.NotFound(new { error = $"NewsSource with Id={id} not found." });
            }

            var source = NewsSourceDto.FromReader(reader);
            return Results.Ok(source);
        }

        private static async Task<IResult> DeleteSourceAsync(string connectionString, int id)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                    DELETE FROM NewsSource
                    WHERE Id = @Id;";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", id);

            var affectedRows = await command.ExecuteNonQueryAsync();

            if (affectedRows == 0)
            {
                return Results.NotFound(new { error = $"NewsSource with Id={id} not found." });
            }

            return Results.NoContent();
        }
    }
}