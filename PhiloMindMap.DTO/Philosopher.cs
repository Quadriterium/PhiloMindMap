using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhiloMindMap.DTO
{
    public class Philosopher
    {
        public string Description { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public long Id { get; set; }

        public DateTime BirthDate { get; set; }

        public DateTime? DeathDate { get; set; }

        public string? ProfileImageUrl { get; set; }

        public double PositionX { get; set; }

        public double PositionY { get; set; }
    }
}
