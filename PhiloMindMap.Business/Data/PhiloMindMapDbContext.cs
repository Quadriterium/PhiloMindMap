using Microsoft.EntityFrameworkCore;
using PhiloMindMap.DTO;

namespace PhiloMindMap.Business.Data
{
    public class PhiloMindMapDbContext : DbContext
    {
        public PhiloMindMapDbContext(DbContextOptions<PhiloMindMapDbContext> options)
            : base(options)
        {
        }

        public DbSet<Philosopher> Philosophers => Set<Philosopher>();

        public DbSet<Idea> Ideas => Set<Idea>();

        public DbSet<PhilosopherIdeaLink> PhilosopherIdeaLinks => Set<PhilosopherIdeaLink>();

        public DbSet<MindMapContent> MindMapContents => Set<MindMapContent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Philosopher>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name).IsRequired();
                entity.Property(x => x.Description).IsRequired();
                entity.Property(x => x.ProfileImageUrl).IsRequired(false);
                entity.Property(x => x.PositionX).HasDefaultValue(0d);
                entity.Property(x => x.PositionY).HasDefaultValue(0d);
                entity.HasIndex(x => x.Name).IsUnique();
            });

            modelBuilder.Entity<Idea>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name).IsRequired();
                entity.Property(x => x.PositionX).HasDefaultValue(0d);
                entity.Property(x => x.PositionY).HasDefaultValue(0d);
                entity.HasIndex(x => x.Name).IsUnique();
            });

            modelBuilder.Entity<PhilosopherIdeaLink>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.PhilosopherId, x.IdeaId }).IsUnique();
            });

            modelBuilder.Entity<MindMapContent>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ContentKey).IsRequired();
                entity.Property(x => x.Title).IsRequired();
                entity.Property(x => x.HtmlContent).IsRequired();
                entity.HasIndex(x => x.ContentKey).IsUnique();
            });
        }
    }
}
