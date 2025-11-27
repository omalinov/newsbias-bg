using Microsoft.Data.SqlClient;

namespace News_Platform
{
    public record UserPreferenceDto(
        int Id,
        int UserId,
        string? Topic
    )
    {
        public static UserPreferenceDto FromReader(SqlDataReader reader)
        {
            return new UserPreferenceDto(
                Id: reader.GetInt32(0),
                UserId: reader.GetInt32(1),
                Topic: reader.IsDBNull(2) ? null : reader.GetString(2)
            );
        }
    }

    public record CreateUserPreferenceRequest(
        string Topic
    )
    {
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

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(Topic))
            {
                error = "Topic is required.";
                return false;
            }

            if (NormalizedTopic is { Length: > 100 })
            {
                error = "Topic must be maximum 100 chars.";
                return false;
            }

            error = "";
            return true;
        }

        public void FillSqlParameters(SqlCommand command, int userId)
        {
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Topic", NormalizedTopic ?? (object)DBNull.Value);
        }
    }
    public static class UserPreferenceEndpoints
    {
        public static void MapUserPreferenceEndpoints(this WebApplication app, string connectionString)
        {
            app.MapGet("/users/{userId:int}/preferences",
                (int userId) => GetPreferencesForUserAsync(connectionString, userId));

            app.MapPost("/users/{userId:int}/preferences",
                (int userId, CreateUserPreferenceRequest request) =>
                    CreatePreferenceAsync(connectionString, userId, request));

            app.MapDelete("/users/{userId:int}/preferences/{prefId:int}",
                (int userId, int prefId) =>
                    DeletePreferenceAsync(connectionString, userId, prefId));
        }

        private static async Task<IResult> GetPreferencesForUserAsync(string connectionString, int userId)
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
                SELECT Id, UserId, Topic
                FROM UserPreference
                WHERE UserId = @UserId
                ORDER BY Id;";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);

            var prefs = new List<UserPreferenceDto>();
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                prefs.Add(UserPreferenceDto.FromReader(reader));
            }

            return Results.Ok(prefs);
        }

        private static async Task<IResult> CreatePreferenceAsync(
            string connectionString,
            int userId,
            CreateUserPreferenceRequest request)
        {
            if (!request.TryValidate(out var error))
            {
                return Results.BadRequest(new { error });
            }

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
                INSERT INTO UserPreference (UserId, Topic)
                OUTPUT inserted.Id,
                        inserted.UserId,
                        inserted.Topic
                VALUES (@UserId, @Topic);";

            await using var command = new SqlCommand(sql, connection);
            request.FillSqlParameters(command, userId);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Results.Problem("Failed to insert UserPreference.");
            }

            var pref = UserPreferenceDto.FromReader(reader);
            return Results.Created($"/users/{userId}/preferences/{pref.Id}", pref);
        }

        private static async Task<IResult> DeletePreferenceAsync(string connectionString, int userId, int prefId)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                    DELETE FROM UserPreference
                    WHERE Id = @Id AND UserId = @UserId;";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", prefId);
            command.Parameters.AddWithValue("@UserId", userId);

            var affectedRows = await command.ExecuteNonQueryAsync();

            if (affectedRows == 0)
            {
                return Results.NotFound(new
                {
                    error = $"UserPreference with Id={prefId} for AppUser {userId} not found."
                });
            }

            return Results.NoContent();
        }
    }
}