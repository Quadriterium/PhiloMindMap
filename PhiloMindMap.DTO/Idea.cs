namespace PhiloMindMap.DTO
{
    public class Idea
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public double PositionX { get; set; }

        public double PositionY { get; set; }
    }
}
