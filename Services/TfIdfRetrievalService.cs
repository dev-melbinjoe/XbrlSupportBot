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
            public double KeywordScore { get; init; }
            public double TfidfScore { get; init; }
        }

        // Tunable weights
        private const double AlphaKeyword = 0.65; // weight for keyword/phrase match
        private const double BetaTfidf = 0.35;  // weight for tf-idf cosine

        // Candidate pool sizes
        private const int MaxKeywordCandidates = 50; // how many keyword hits to consider
        private const int MaxReturn = 5;             // default topK, override via param

        public TfIdfRetrievalService(IConfiguration config) => _config = config;

        public async Task<List<RetrieveResult>> RetrieveHybrid(string query, int topK = MaxReturn, bool useFullText = true)
        {
            var connStr = _config.GetConnectionString("XbrlDb")!;
            using var conn = new SqlConnection(connStr);

            // Build TF-IDF query vector once
            var (qvec, _) = await _tfidfSvc.BuildQueryVectorAsync(connStr, query);

            // ---- 1) Keyword candidates (FullText or LIKE) ----
            var kwCandidates = useFullText
                ? await KeywordCandidatesFullText(conn, query, MaxKeywordCandidates)
                : await KeywordCandidatesLike(conn, query, MaxKeywordCandidates);

            // If keyword produced nothing (e.g., FT not configured), fall back to "all with embeddings"
            if (kwCandidates.Count == 0)
            {
                kwCandidates = (await conn.QueryAsync<(long Id, long DocId, string Text, byte[] Emb)>(
                    "SELECT TOP 200 Id, DocId, ChunkText AS Text, Embedding AS Emb " +
                    "FROM DocChunks WHERE Embedding IS NOT NULL ORDER BY Id DESC")).ToList();
            }

            // ---- 2) Score TF-IDF cosine for the candidates ----
            var scored = new List<RetrieveResult>(kwCandidates.Count);
            foreach (var r in kwCandidates)
            {
                if (r.Emb == null || r.Emb.Length == 0) continue;

                var vec = TfidfEmbeddingService.DeserializeVector(r.Emb);
                if (vec.Length != qvec.Length) continue; // stale vectors, rebuild needed

                // dot product (both vectors L2-normalized)
                double tfidfScore = 0;
                for (int i = 0; i < qvec.Length; i++)
                    tfidfScore += qvec[i] * vec[i];

                // Combine with keywordScore using weights
                var keywordScore = KeywordMatchScore(r.Text, query);
                var final = AlphaKeyword * keywordScore + BetaTfidf * tfidfScore;

                scored.Add(new RetrieveResult
                {
                    DocId = r.DocId,
                    ChunkTxt = r.Text,
                    TfidfScore = tfidfScore,
                    KeywordScore = keywordScore,
                    Score = final
                });
            }

            return scored
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .ToList();
        }

        // -------- Keyword candidate fetchers --------

        private async Task<List<(long Id, long DocId, string Text, byte[] Emb)>> KeywordCandidatesFullText(
            SqlConnection conn, string rawQuery, int topN)
        {
            try
            {
                // Build FT predicate: exact phrase + AND terms
                var (phrase, terms) = ExtractPhraseAndTerms(rawQuery);

                // Sample FT search pattern:
                // 1) If phrase exists: CONTAINS(ChunkText, "\"contextRef missing\"")
                // 2) AND/OR combine with terms: AND (CONTAINS(ChunkText, 'contextRef AND missing'))
                var where = new List<string>();
                var sqlParams = new DynamicParameters();

                if (!string.IsNullOrWhiteSpace(phrase))
                {
                    where.Add("CONTAINS(ChunkText, @phrase)");
                    sqlParams.Add("@phrase", $"\"{phrase}\"");
                }
                if (terms.Count > 0)
                {
                    var andExpr = string.Join(" AND ", terms.Select(t => $"\"{t}\""));
                    where.Add($"CONTAINS(ChunkText, @andExpr)");
                    sqlParams.Add("@andExpr", andExpr);
                }

                var whereClause = where.Count > 0 ? string.Join(" AND ", where) : "1=1";

                var sql =
                    $"SELECT TOP (@topN) Id, DocId, ChunkText AS Text, Embedding AS Emb " +
                    $"FROM DocChunks WHERE {whereClause}";

                sqlParams.Add("@topN", topN);

                var rows = (await conn.QueryAsync<(long Id, long DocId, string Text, byte[] Emb)>(sql, sqlParams)).ToList();
                return rows;
            }
            catch
            {
                throw;
            }
        }

        private async Task<List<(long Id, long DocId, string Text, byte[] Emb)>> KeywordCandidatesLike(
            SqlConnection conn, string rawQuery, int topN)
        {
            var (phrase, terms) = ExtractPhraseAndTerms(rawQuery);
            var where = new List<string>();
            var sqlParams = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(phrase))
            {
                where.Add("ChunkText LIKE @p");
                sqlParams.Add("@p", $"%{phrase}%");
            }
            foreach (var (t, idx) in terms.Select((t, i) => (t, i)))
            {
                var name = $"@t{idx}";
                where.Add($"ChunkText LIKE {name}");
                sqlParams.Add(name, $"%{t}%");
            }

            var whereClause = where.Count > 0 ? string.Join(" AND ", where) : "1=1";
            var sql =
                $"SELECT TOP (@topN) Id, DocId, ChunkText AS Text, Embedding AS Emb " +
                $"FROM DocChunks WHERE {whereClause}";

            sqlParams.Add("@topN", topN);

            var rows = (await conn.QueryAsync<(long Id, long DocId, string Text, byte[] Emb)>(sql, sqlParams)).ToList();
            return rows;
        }

        // -------- Keyword scoring & helpers --------

        private static (string phrase, List<string> terms) ExtractPhraseAndTerms(string raw)
        {
            raw = raw?.Trim() ?? string.Empty;

            string phrase = string.Empty;
            var terms = new List<string>();

            // If the query has quotes, treat the text inside the first pair as a phrase
            var firstQuote = raw.IndexOf('"');
            var lastQuote = raw.LastIndexOf('"');
            if (firstQuote >= 0 && lastQuote > firstQuote)
            {
                phrase = raw.Substring(firstQuote + 1, lastQuote - firstQuote - 1).Trim();
                var before = raw.Substring(0, firstQuote);
                var after = raw.Substring(lastQuote + 1);
                terms.AddRange(TokenizeTerms(before));
                terms.AddRange(TokenizeTerms(after));
            }
            else
            {
                // No quotes: treat the raw as a bag of terms
                terms.AddRange(TokenizeTerms(raw));
            }

            // keep distinct non-empty terms
            terms = terms.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return (phrase, terms);
        }

        private static IEnumerable<string> TokenizeTerms(string s)
        {
            return (s ?? string.Empty)
                .ToLowerInvariant()
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 2);
        }

        private static double KeywordMatchScore(string text, string rawQuery)
        {
            text ??= string.Empty;
            rawQuery ??= string.Empty;

            // basic, fast scoring:
            // +1.0 if exact phrase occurs
            // +0.15 per matching term (cap at +0.45)
            var (phrase, terms) = ExtractPhraseAndTerms(rawQuery);

            double score = 0.0;
            var lowText = text.ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(phrase) && lowText.Contains(phrase.ToLowerInvariant()))
                score += 1.0;

            int termMatches = terms.Count(t => lowText.Contains(t));
            score += Math.Min(termMatches * 0.15, 0.45); // cap term contribution

            return Math.Min(score, 1.0);
        }
    }
}