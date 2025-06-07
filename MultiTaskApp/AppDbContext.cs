using Microsoft.EntityFrameworkCore;
using MultiTaskApp.Models;

namespace MultiTaskApp.Data
{
    public class ApplicationContext : DbContext
    {
        public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options) { }

        // DbSets para as entidades
        public DbSet<Entity> Entities { get; set; }
        public DbSet<Fact> Facts { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<Value> Values { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configuração de Entity
            modelBuilder.Entity<Entity>()
                .HasKey(e => e.Id);

            modelBuilder.Entity<Entity>()
                .HasMany(e => e.Facts)
                .WithOne(f => f.Entity)
                .HasForeignKey(f => f.EntityId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configuração de Fact
            modelBuilder.Entity<Fact>()
                .HasKey(f => f.Id);

            modelBuilder.Entity<Fact>()
                .HasMany(f => f.Units)
                .WithOne(u => u.Fact)
                .HasForeignKey(u => u.FactId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configuração de Unit
            modelBuilder.Entity<Unit>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<Unit>()
                .HasMany(u => u.Values)
                .WithOne(v => v.Unit)
                .HasForeignKey(v => v.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configuração de Value
            modelBuilder.Entity<Value>()
                .HasKey(v => v.Id);
        }
    }
}