using Avixar.Entity;
using Avixar.Entity.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Avixar.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<UserAddress> UserAddresses { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            // 3. User Addresses
            builder.Entity<UserAddress>(entity =>
            {
                entity.ToTable("user_addresses");
                
                // Primary Key & Defaults
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

                // --- COLUMNS & C# VALIDATION ---
                
                entity.Property(a => a.Label)
                      .HasMaxLength(300);

                entity.Property(a => a.AddressLine1)
                      .IsRequired()
                      .HasMaxLength(300); // Maps to varchar(300) OR enforces length validation

                entity.Property(a => a.AddressLine2)
                      .HasMaxLength(300);

                entity.Property(a => a.City)
                      .IsRequired()
                      .HasMaxLength(300);

                entity.Property(a => a.PostalCode)
                      .IsRequired()
                      .HasMaxLength(300);

                // --- SQL CHECK CONSTRAINTS ---
                // Mapping the raw SQL constraints so EF knows they exist
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("Chk_Label_Len",  "LENGTH(\"Label\") <= 300");
                    t.HasCheckConstraint("Chk_Addr1_Len",  "LENGTH(\"AddressLine1\") <= 300");
                    t.HasCheckConstraint("Chk_Addr2_Len",  "LENGTH(\"AddressLine2\") <= 300");
                    t.HasCheckConstraint("Chk_City_Len",   "LENGTH(\"City\") <= 300");
                    t.HasCheckConstraint("Chk_Postal_Len", "LENGTH(\"PostalCode\") <= 300");
                });

                // Relationships
                entity.HasOne(a => a.User)
                      .WithMany(u => u.Addresses)
                      .HasForeignKey(a => a.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.State)
                      .WithMany()
                      .HasForeignKey(a => a.StateId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Country)
                      .WithMany()
                      .HasForeignKey(a => a.CountryIsoCode)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
