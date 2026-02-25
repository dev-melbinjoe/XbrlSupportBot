using System.Data.SqlClient;
using Dapper;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Primitives;

namespace XbrlSupportBot.Services
{
    public class TfIdfEmbeddingService
    {
        public Dictionary<string, int> Vocabulary = new();
        public List<double[]> Vectors = new();
        public List<long> ChunkIds = new();

        public async Task BuildandStoreEmbeddings(string connStr)
        {
            try
            {
                using var conn = new SqlConnection(connStr);
                // 1. Load all chunks
                var chunks = (await conn.QueryAsync<(long Id, string Chunktext)>(
                    "SELECT Id, ChunkText FROM DocChunks ORDER BY Id ASC")).ToList();


                // 2. Build Vocabulary

                foreach (var chunk in chunks)
                {
                    foreach (var token in Tokenize(chunk.Chunktext))
                    {
                        if (!Vocabulary.ContainsKey(token))
                            Vocabulary[token] = Vocabulary.Count; 
                        
                    }
                }

                // 3. Build Vectors

                foreach (var chunk in chunks) {

                    var vec = new double[Vocabulary.Count];

                    foreach (var token in Tokenize(chunk.Chunktext)) {
                        vec[Vocabulary[token]]++;
                    }
                    Vectors.Add(vec);
                    ChunkIds.Add(chunk.Id);

                    // 4. Store vectors in sql as VARBINARY

                    for (int i = 0;i < ChunkIds.Count ; i++)
                    {
                        var bin = SerializeVector(Vectors[i]);
                        await conn.ExecuteAsync("UPDATE DocChunks SET Embedding = @v WHERE Id = @id", new {v = bin, id = ChunkIds[i] });
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        private IEnumerable<string> Tokenize(string chunktext) {

            var cleaned = Regex.Replace(chunktext.ToLower(), @"[^a-z0-9\s]", " ");

            return cleaned.Split(" ",StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 2);
        }

        public static byte[] SerializeVector(double[] vector)
        {
            var bytes = new byte[vector.Length * sizeof(double)];
            Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
