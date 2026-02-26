using Microsoft.Data.SqlClient;
using Dapper;

namespace XbrlSupportBot.Services
{
    public class TfIdfRetrievalService
    {
        private readonly IConfiguration _config;
        private readonly TfidfEmbeddingService _tfidfSvc = new();

        public sealed record RetrieveResult
        {
            public long DocId { get; init; }
            public string ChunkTxt { get; init; } = "";
            public double Score { get; init; }
        }

        public TfIdfRetrievalService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<List<RetrieveResult>> Retrieve(string query, int topK = 5)
        {
            var connStr = _config.GetConnectionString("XbrlDb")!;
            using var conn = new SqlConnection(connStr);

            // 1) Build query vector using the persisted TF-IDF vocab + IDF
            var (qvec, _) = await _tfidfSvc.BuildQueryVectorAsync(connStr, query);

            // 2) Load candidate chunks that already have embeddings
            var rows = (await conn.QueryAsync<(long Id, long DocId, string Text, byte[] Emb)>(
                "SELECT Id, DocId, ChunkText AS Text, Embedding AS Emb FROM DocChunks WHERE Embedding IS NOT NULL"
            )).ToList();

            // 3) Cosine similarity (dot product of L2-normalized vectors)
            var results = new List<RetrieveResult>(rows.Count);
            foreach (var r in rows)
            {
                if (r.Emb == null || r.Emb.Length == 0) continue;

                var vec = TfidfEmbeddingService.DeserializeVector(r.Emb);

                // If sizes don't match, it means vocabulary changed: rebuild embeddings
                if (vec.Length != qvec.Length) continue;

                double dot = 0;
                for (int i = 0; i < qvec.Length; i++)
                    dot += qvec[i] * vec[i];

                results.Add(new RetrieveResult
                {
                    DocId = r.DocId,
                    ChunkTxt = r.Text,
                    Score = dot
                });
            }

            return results
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .ToList();
        }
    }
}