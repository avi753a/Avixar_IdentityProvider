using Microsoft.EntityFrameworkCore;

namespace Avixar.IdP.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // We might add DbSets here later for read-only access
        // public DbSet<CoreUser> CoreUsers { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Map to PostgreSQL types if needed
            modelBuilder.HasPostgresExtension("uuid-ossp");
            modelBuilder.HasPostgresExtension("pgcrypto");
        }
    }
}
