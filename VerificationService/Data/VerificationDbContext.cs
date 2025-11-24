using Microsoft.EntityFrameworkCore;
using VerificationService.Models;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace VerificationService.Data
{
    public class VerificationDbContext : DbContext
    {
        public VerificationDbContext(DbContextOptions<VerificationDbContext> options) : base(options) { }

        public DbSet<PendingOrder> PendingOrders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PendingOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ItemsJson).IsRequired();
                entity.HasIndex(e => e.ReservedAt);
                entity.Property(e => e.Status).HasConversion<string>();
            });
        }
    }
}