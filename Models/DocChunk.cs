namespace XbrlSupportBot.Models
{
    public class DocChunk
    {

        public long Id { get; set; }
        public long DocId { get; set; }
        public int ChunkIndex { get; set; }
        public string ChunkText { get; set; } = default!;
        public string? Metadata { get; set; }
        public byte[]? Embedding { get; set; }  // VARBINARY(MAX)
        public DateTime CreatedAt { get; set; }

    }
}
