using Microsoft.Data.SqlClient;
using System.Net.Mail;

namespace News_Platform
{
    public record AppUserDto(
        int Id,
        string Name,
        string Email,
        string PoliticalPreference
    )
    {
        public static AppUserDto FromReader(SqlDataReader reader)
        {
            return new AppUserDto(
                Id: reader.GetInt32(0),
                Name: reader.GetString(1),
                Email: reader.GetString(2),
                PoliticalPreference: reader.GetString(3)
            );
        }
    }
    public static class AppUserConstants
    {
        public static readonly string[] AllowedPoliticalPreferences =
        {
            "pro-eu",
            "pro-russia",
            "neutral",
            "unknown",
            "none"
        };
    }
    public record CreateAppUserRequest(
        string Name,
        string Email,
        string? PoliticalPreference
    )
    {
        public string NormalizedName
        {
            get
            {
                return Name.Trim();
            }
        }

        public string NormalizedEmail
        {
            get
            {
                return Email.Trim().ToLowerInvariant();
            }
        }

        public string NormalizedPoliticalPreference
        {
            get
            {
                return string.IsNullOrWhiteSpace(PoliticalPreference)
                    ? "none"
                    : PoliticalPreference.Trim().ToLowerInvariant();
            }
        }
        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                error = "Name is required.";
                return false;
            }

            if (NormalizedName.Length > 200)
            {
                error = "Name must be maximum 200 chars.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                error = "Email is required.";
                return false;
            }

            try
            {
                var _ = new MailAddress(Email.Trim());
            }
            catch
            {
                error = "Invalid email format.";
                return false;
            }

            if (NormalizedEmail.Length > 200)
            {
                error = "Email must be maximum 200 chars.";
                return false;
            }

            if (!AppUserConstants.AllowedPoliticalPreferences.Contains(NormalizedPoliticalPreference))
            {
                error = "Invalid PoliticalPreference. Allowed values: none, pro-eu, pro-russia, neutral, unknown.";
                return false;
            }

            error = "";
            return true;
        }

        public void FillSqlParameters(SqlCommand command)
        {
            command.Parameters.AddWithValue("@Name", NormalizedName);
            command.Parameters.AddWithValue("@Email", NormalizedEmail);
            command.Parameters.AddWithValue("@PoliticalPreference", NormalizedPoliticalPreference);
        }
    }

    public static class AppUserEndpoints
    {
        public static void MapAppUserEndpoints(this WebApplication app, string connectionString)
        {
            app.MapPost("/users", (CreateAppUserRequest request) => CreateAppUserAsync(connectionString, request));
            app.MapGet("/users", () => ReadAllAppUserAsync(connectionString));
            app.MapGet("/users/{id:int}", (int id) => ReadSingleAppUserAsync(connectionString, id));
            app.MapPut("/users/{id:int}", (int id, CreateAppUserRequest request) => UpdateAppUserAsync(connectionString, id, request));
            app.MapDelete("/users/{id:int}", (int id) => DeleteAppUserAsync(connectionString, id));
        }
        private static async Task<IResult> CreateAppUserAsync(string connectionString, CreateAppUserRequest request)
        {
            if (!request.TryValidate(out var error))
            {
                return Results.BadRequest(new { error });
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
               INSERT INTO AppUser (
                    Name,
                    Email,
                    PoliticalPreference
               )
                OUTPUT inserted.Id,
                       inserted.Name,
                       inserted.Email,
                       inserted.PoliticalPreference
                VALUES (
                    @Name,
                    @Email,
                    @PoliticalPreference
                );
            ";

            await using var command = new SqlCommand(sql, connection);
            request.FillSqlParameters(command);

            try
            {
                await using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return Results.Problem("Failed to insert AppUser.");
                }

                var user = AppUserDto.FromReader(reader);
                return Results.Created($"/users/{user.Id}", user);
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return Results.BadRequest(new { error = "Email already exists." });
            }
        }

        private static async Task<IResult> ReadAllAppUserAsync(string connectionString)
        {
            var users = new List<AppUserDto>();

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT Id, Name, Email, PoliticalPreference
                FROM AppUser
                ORDER BY Id";

            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var user = AppUserDto.FromReader(reader);
                users.Add(user);
            }

            return Results.Ok(users);
        }

        private static async Task<IResult> ReadSingleAppUserAsync(string connectionString, int id)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT Id, Name, Email, PoliticalPreference
                FROM AppUser
                WHERE Id = @Id";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", id);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Results.NotFound(new { error = $"AppUser with Id={id} not found." });
            }

            var user = AppUserDto.FromReader(reader);
            return Results.Ok(user);
        }

        private static async Task<IResult> UpdateAppUserAsync(string connectionString, int id, CreateAppUserRequest request)
        {
            if (!request.TryValidate(out var error))
            {
                return Results.BadRequest(new { error });
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                UPDATE AppUser
                SET Name = @Name,
                    Email = @Email,
                    PoliticalPreference = @PoliticalPreference
                OUTPUT inserted.Id,
                        inserted.Name,
                        inserted.Email,
                        inserted.PoliticalPreference
                WHERE Id = @Id;";

            await using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Id", id);
            request.FillSqlParameters(command);

            try
            {
                await using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return Results.NotFound(new { error = $"AppUser with Id={id} not found." });
                }

                var user = AppUserDto.FromReader(reader);
                return Results.Ok(user);
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return Results.BadRequest(new { error = "Email already exists." });
            }
        }

        private static async Task<IResult> DeleteAppUserAsync(string connectionString, int id)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                    DELETE FROM AppUser
                    WHERE Id = @Id;";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", id);

            var affectedRows = await command.ExecuteNonQueryAsync();

            if (affectedRows == 0)
            {
                return Results.NotFound(new { error = $"AppUser with Id={id} not found." });
            }

            return Results.NoContent();
        }
    }
}
