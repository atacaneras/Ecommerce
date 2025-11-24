using Microsoft.EntityFrameworkCore;
using VerificationService.Models;

namespace VerificationService.Data
{
    public class VerificationDbContext : DbContext
    {
        public VerificationDbContext(DbContextOptions<VerificationDbContext> options) : base(options) { }

        public DbSet<Verification> Verifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Verification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerName).HasMaxLength(200);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.HasIndex(e => e.OrderId);
                entity.HasIndex(e => e.Status);
            });
        }
    }
}