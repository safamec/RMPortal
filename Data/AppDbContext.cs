using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) {}
    public DbSet<MediaAccessRequest> Requests => Set<MediaAccessRequest>();
    public DbSet<RequestDecision> RequestDecisions => Set<RequestDecision>();
}
