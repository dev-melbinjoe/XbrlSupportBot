namespace XbrlSupportBot.Models
{
    public class Documents
    {

        public long Id { get; set; }
        public string DocType { get; set; } = default!;
        public string? Title { get; set; }
        public string Content { get; set; } = default!;
        public string? SourceUri { get; set; }
        public string? Module { get; set; }
        public string? Tags { get; set; }
        public string? AclLabel { get; set; } = "support";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

    }
}
