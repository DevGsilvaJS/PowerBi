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
    public DbSet<ComparativoFinanceiroSnapshotPagar> ComparativoFinanceiroSnapshotsPagar =>
        Set<ComparativoFinanceiroSnapshotPagar>();
    public DbSet<ComparativoFinanceiroSnapshotReceber> ComparativoFinanceiroSnapshotsReceber =>
        Set<ComparativoFinanceiroSnapshotReceber>();

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

        modelBuilder.Entity<ComparativoFinanceiroSnapshotPagar>(entity =>
        {
            entity.ToTable("comparativo_financeiro_snapshot_pagar");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.SerieJson).HasColumnType("text");
            entity.Property(e => e.FormasJson).HasColumnType("text");
            entity.Property(e => e.AtualizadoEmUtc)
                .HasDefaultValueSql("timezone('utc', now())");
            entity.HasIndex(e => new { e.GestaoClienteId, e.AnoMenor, e.AnoMaior, e.LojaParam })
                .IsUnique()
                .HasDatabaseName("ux_cmp_fin_pagar_cliente_anos_loja");
            entity.HasOne(e => e.GestaoCliente)
                .WithMany()
                .HasForeignKey(e => e.GestaoClienteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ComparativoFinanceiroSnapshotReceber>(entity =>
        {
            entity.ToTable("comparativo_financeiro_snapshot_receber");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.SerieJson).HasColumnType("text");
            entity.Property(e => e.FormasJson).HasColumnType("text");
            entity.Property(e => e.AtualizadoEmUtc)
                .HasDefaultValueSql("timezone('utc', now())");
            entity.HasIndex(e => new { e.GestaoClienteId, e.AnoMenor, e.AnoMaior, e.LojaParam })
                .IsUnique()
                .HasDatabaseName("ux_cmp_fin_receber_cliente_anos_loja");
            entity.HasOne(e => e.GestaoCliente)
                .WithMany()
                .HasForeignKey(e => e.GestaoClienteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
