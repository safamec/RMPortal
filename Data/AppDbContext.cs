using Microsoft.EntityFrameworkCore;
using RMPortal.Models;

namespace RMPortal.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<MediaAccessRequest> Requests { get; set; } = default!;
        public DbSet<RequestDecision> RequestDecisions { get; set; } = default!;

       protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<MediaAccessRequest>().ToTable("Requests");
    modelBuilder.Entity<RequestDecision>().ToTable("RequestDecisions");

    modelBuilder.Entity<RequestDecision>()
        .HasOne(d => d.MediaAccessRequest)
        .WithMany(r => r.Decisions)
        .HasForeignKey(d => d.MediaAccessRequestId)
        .OnDelete(DeleteBehavior.Cascade);
}

    }
}
