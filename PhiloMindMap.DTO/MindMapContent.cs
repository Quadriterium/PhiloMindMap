namespace PhiloMindMap.DTO
{
    public class MindMapContent
    {
        public long Id { get; set; }

        public string ContentKey { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string HtmlContent { get; set; } = string.Empty;
    }
}
