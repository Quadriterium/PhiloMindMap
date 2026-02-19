namespace PhiloMindMap.DTO
{
    public class PhilosopherIdeaLink
    {
        public long Id { get; set; }

        public long PhilosopherId { get; set; }

        public long IdeaId { get; set; }

        public string? RelationType { get; set; }
    }
}
