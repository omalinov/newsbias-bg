using Microsoft.Data.SqlClient;

namespace News_Platform
{
    public static class NewsSourceRepository
    {
        public static async Task<NewsSourceDto?> GetSourceByIdAsync(SqlConnection connection, int id)
        {
            const string sql = @"
                SELECT Id, Name, Url, Category, IsActive, PoliticalLeaning, CreatedAt
                FROM NewsSource
                WHERE Id = @Id";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", id);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            return NewsSourceDto.FromReader(reader);
        }
    }
}
