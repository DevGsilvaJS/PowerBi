using Microsoft.EntityFrameworkCore;
using PowerBi.Server.Entities;

namespace PowerBi.Server.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<GestaoCliente> GestaoClientes => Set<GestaoCliente>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<GestaoCliente>(entity =>
        {
            entity.ToTable("gestaoclientes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.CriadoEm)
                .HasDefaultValueSql("timezone('utc', now())");
        });
    }
}
