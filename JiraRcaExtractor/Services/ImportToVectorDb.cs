namespace JiraExtractor.Services
{
    public class ImportToVectorDb
    {
        static async Task ImportToPostgres()
        {
            // Note: You need a Vector Embedding service (like OpenAI or Ollama) 
            // to turn text into the 'Vector' type for pgvector.
            Console.WriteLine("Reading verified Excel...");
            using var workbook = new XLWorkbook(ExcelPath);
            var rows = workbook.Worksheet(1).RowsUsed().Skip(1);

            using var conn = new NpgsqlConnection("Host=localhost;Database=my_vector_db;Username=postgres;Password=pass");
            await conn.OpenAsync();

            foreach (var row in rows)
            {
                var summary = row.Cell(2).Value.ToString();
                var rca = row.Cell(4).Value.ToString();

                // Placeholder: Vector embedding generation logic goes here
                // var vector = await GetEmbedding(summary + rca); 

                string sql = "INSERT INTO jira_vectors (ticket_key, content) VALUES (@k, @c)";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("k", row.Cell(1).Value.ToString());
                cmd.Parameters.AddWithValue("c", rca);
                await cmd.ExecuteNonQueryAsync();
            }
            Console.WriteLine("Import Complete.");
        }
    }
}
