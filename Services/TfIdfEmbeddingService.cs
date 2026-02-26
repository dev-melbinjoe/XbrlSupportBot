using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Text.RegularExpressions;

namespace XbrlSupportBot.Services
{
    public class TfidfEmbeddingService
    {
        // Config
        private readonly int _maxVocab;           // cap vocab size (e.g., 20k)
        private readonly HashSet<string> _stop;

        public TfidfEmbeddingService(int maxVocab = 20000)
        {
            _maxVocab = maxVocab;
            _stop = new HashSet<string>(new[] {
                "the","and","for","with","that","this","from","into","then","when",
                "are","was","were","will","shall","can","could","should","would",
                "has","have","had","been","being","a","an","of","to","in","on","at",
                "by","as","or","if","is","it","its","be","not","no","yes","you","your"
            });
        }

        // Tokenizer: lowercase, alphanum only, >2 chars, stopwords removed
        public IEnumerable<string> Tokenize(string text)
        {
            var cleaned = Regex.Replace((text ?? string.Empty).ToLowerInvariant(), @"[^a-z0-9\s]", " ");
            foreach (var t in cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (t.Length > 2 && !_stop.Contains(t))
                    yield return t;
            }
        }

        public static byte[] SerializeVector(double[] vector)
        {
            var bytes = new byte[vector.Length * sizeof(double)];
            Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static double[] DeserializeVector(byte[] bin)
        {
            var vec = new double[bin.Length / sizeof(double)];
            Buffer.BlockCopy(bin, 0, vec, 0, bin.Length);
            return vec;
        }

        // L2 normalize
        private static void Normalize(double[] v)
        {
            double sum = 0;
            for (int i = 0; i < v.Length; i++) sum += v[i] * v[i];
            var norm = Math.Sqrt(sum) + 1e-12;
            for (int i = 0; i < v.Length; i++) v[i] /= norm;
        }

        /// <summary>
        /// Builds TF-IDF vocabulary (with DF/IDF), persists it in SQL,
        /// then builds TF-IDF + L2-normalized vectors for all chunks and stores them in DocChunks.Embedding.
        /// </summary>
        public async Task BuildTfIdfAsync(string connectionString)
        {
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // 1) Read all chunks
            var chunks = (await conn.QueryAsync<(long Id, string Text)>(
                "SELECT Id, ChunkText AS Text FROM DocChunks ORDER BY Id ASC"
            )).ToList();

            var N = chunks.Count;
            if (N == 0) return;

            // 2) Compute document frequencies (DF)
            var df = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var c in chunks)
            {
                var terms = new HashSet<string>(Tokenize(c.Text));
                foreach (var t in terms)
                {
                    if (!df.TryAdd(t, 1)) df[t]++;
                }
            }

            // 3) Select top vocabulary (by DF, capped to _maxVocab)
            var vocabTerms = df.OrderByDescending(kv => kv.Value)
                               .Take(_maxVocab)
                               .Select((kv, i) => new { kv.Key, kv.Value, Index = i })
                               .ToList();

            // 4) Compute IDF for vocab
            // idf = log((N + 1) / (df + 1)) + 1
            var idf = new double[vocabTerms.Count];
            for (int i = 0; i < vocabTerms.Count; i++)
                idf[i] = Math.Log((N + 1.0) / (vocabTerms[i].Value + 1.0)) + 1.0;

            // 5) Persist vocab & meta (reset tables each rebuild)
            await conn.ExecuteAsync("TRUNCATE TABLE TfidfVocab;");
            var vocabInsertSql = "INSERT INTO TfidfVocab (Term, TermIndex, Df, Idf) VALUES (@Term, @TermIndex, @Df, @Idf)";
            foreach (var vt in vocabTerms)
                await conn.ExecuteAsync(vocabInsertSql, new { Term = vt.Key, TermIndex = vt.Index, Df = vt.Value, Idf = idf[vt.Index] });

            await conn.ExecuteAsync("INSERT INTO TfidfMeta (VocabSize, Notes) VALUES (@VocabSize, @Notes);",
                new { VocabSize = vocabTerms.Count, Notes = "TF-IDF rebuild" });

            // 6) Map term -> index for fast lookup
            var termToIndex = vocabTerms.ToDictionary(x => x.Key, x => x.Index, StringComparer.Ordinal);

            // 7) Build TF-IDF for each chunk, L2 normalize, store as VARBINARY
            foreach (var c in chunks)
            {
                var vec = new double[vocabTerms.Count];
                // term frequency
                foreach (var t in Tokenize(c.Text))
                {
                    if (termToIndex.TryGetValue(t, out var idx))
                        vec[idx] += 1.0;
                }
                // TF-IDF
                for (int i = 0; i < vec.Length; i++)
                {
                    if (vec[i] != 0) vec[i] = vec[i] * idf[i];
                }
                Normalize(vec);

                var bin = SerializeVector(vec);
                await conn.ExecuteAsync("UPDATE DocChunks SET Embedding = @E WHERE Id = @Id",
                    new { E = bin, Id = c.Id });
            }
        }

        /// <summary>
        /// Builds a TF-IDF vector for the user query using the persisted vocabulary + IDF.
        /// </summary>
        public async Task<(double[] vec, int size)> BuildQueryVectorAsync(string connectionString, string query)
        {
            using var conn = new SqlConnection(connectionString);
            var vocab = (await conn.QueryAsync<(string Term, int TermIndex, double Idf)>(
                "SELECT Term, TermIndex, Idf FROM TfidfVocab"
            )).ToList();

            var size = vocab.Count;
            var vec = new double[size];
            var idxMap = vocab.ToDictionary(v => v.Term, v => (v.TermIndex, v.Idf), StringComparer.Ordinal);

            foreach (var t in Tokenize(query))
            {
                if (idxMap.TryGetValue(t, out var meta))
                    vec[meta.TermIndex] += 1.0;
            }
            // multiply by IDF
            foreach (var kv in idxMap)
            {
                int idx = kv.Value.TermIndex;
                if (vec[idx] != 0.0)
                    vec[idx] = vec[idx] * kv.Value.Idf;
            }
            Normalize(vec);
            return (vec, size);
        }
    }
}