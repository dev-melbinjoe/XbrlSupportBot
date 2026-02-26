using System.Data.SqlClient;
using Dapper;
using Microsoft.Data.SqlClient;


namespace XbrlSupportBot.Services
{
    public class TfIdfRetrievalService_26022026
    {

        private readonly IConfiguration _config;



        public sealed record RetrieveResult
        {
            public long DocId { get; init; }
            public string ChunkTxt { get; init; } = "";
            public double Score { get; init; }
        }


        public TfIdfRetrievalService_26022026(IConfiguration config)
        {
            _config = config;
        }

        public async Task<List<RetrieveResult>> Retrieve(string query, int topK = 5)
        {
            try
            {
                string connstr = _config.GetConnectionString("XbrlDb");
                using var conn = new SqlConnection(connstr);

                // 1. Load All chunks
                var rows = (await conn.QueryAsync<(long Id, long DocId, string Text, byte[] embd)>("SELECT Id, DocId, ChunkText as Text, Embedding as embd FROM DocChunks")).ToList();

                // 2. Convert Tf - Idf vector

                var svc = new TfIdfEmbeddingService();
                var tokens = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var vec = BuildQueryVector(tokens, rows, svc);


                // 3. Compute similarity
                //var scored = rows.Select(r => (r.DocId, r.Text, Score: Cosine(vec, Deserialize(r.embd)))).OrderByDescending(x => x.Score).Take(topK).ToList();

                var scored = rows
                            .Select(r =>
                                    new RetrieveResult
                                    {
                                        DocId = r.DocId,
                                        ChunkTxt = r.Text,
                                        Score = Cosine(vec, Deserialize(r.embd))
                                    })
                            .OrderByDescending(x => x.Score)
                            .Take(topK)
                            .ToList();

                return scored;

            }
            catch (Exception ex)
            {
                throw;
            }
        }

        //public async Task<List<(long DocId, string ChunkTxt)>> Retrieve(string query, int topK = 5)
        //{
        //    try
        //    {
        //        string connstr = _config.GetConnectionString("XbrlDb");
        //        using var conn = new SqlConnection(connstr);

        //        var rows = (await conn.QueryAsync<(long Id, long DocId, string Text, byte[] embd)>(
        //            "SELECT Id, DocId, ChunkText as Text, Embedding as embd FROM DocChunks")).ToList();

        //        var svc = new TfIdfEmbeddingService();
        //        var tokens = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        //        var vec = BuildQueryVector(tokens, rows, svc);

        //        var top = rows
        //            .Select(r => (
        //                DocId: r.DocId,
        //                Text: r.Text,
        //                Score: Cosine(vec, Deserialize(r.embd))
        //            ))
        //            .OrderByDescending(x => x.Score)
        //            .Take(topK)
        //            .Select(x => (x.DocId, ChunkTxt: x.Text)) // project & rename to match return type
        //            .ToList();

        //        return top;
        //    }
        //    catch
        //    {
        //        throw;
        //    }
        //}



        #region In class Utilities


        private static double[] Deserialize(byte[] bin)
        {
            var vec = new double[bin.Length / sizeof(double)];
            Buffer.BlockCopy(bin, 0, vec, 0, bin.Length);
            return vec;
        }


        private static double[] BuildQueryVector(IEnumerable<string> tokens,
                List<(long Id, long DocId, string Text, byte[] Emb)> rows,
                TfIdfEmbeddingService svc)
        {
            // Build vocabulary from first stored vector length
            int size = Deserialize(rows.First().Emb).Length;
            var vec = new double[size];

            // Very simple TF-only query vector (good enough for support bot)
            foreach (var t in tokens.Where(t => t.Length > 2))
            {
                // If vocabulary dictionary has token, add weight
                // Using ContainsKey or hash approach (not perfect, but simple)
                int hash = Math.Abs(t.GetHashCode()) % size;
                vec[hash]++;
            }
            return vec;
        }


        private static double Cosine(double[] a, double[] b)
        {
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-12);
        }

        #endregion


    }
}
