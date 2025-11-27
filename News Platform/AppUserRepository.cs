using Microsoft.Data.SqlClient;

namespace News_Platform
{
    public class AppUserRepository
    {
        public static async Task<AppUserDto?> GetByIdAsync(SqlConnection connection, int id)
        {
            const string sql = @"
                SELECT Id, Name, Email, PoliticalPreference
                FROM AppUser
                WHERE Id = @Id;";

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", id);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            return AppUserDto.FromReader(reader);
        }
    }
}
