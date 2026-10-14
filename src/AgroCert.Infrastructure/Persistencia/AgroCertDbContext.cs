using AgroCert.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroCert.Infrastructure.Persistencia;

public class AgroCertDbContext : DbContext
{
    public const string Schema = "certificacao";

    // As entidades usam DateTime local; sem isto o Npgsql exige UTC em colunas timestamp.
    static AgroCertDbContext() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    public AgroCertDbContext(DbContextOptions<AgroCertDbContext> options) : base(options) { }

    public DbSet<Certificacao> Certificacoes => Set<Certificacao>();
    public DbSet<Requisito> Requisitos => Set<Requisito>();
    public DbSet<Vistoria> Vistorias => Set<Vistoria>();
    public DbSet<Apontamento> Apontamentos => Set<Apontamento>();
    public DbSet<Propriedade> Propriedades => Set<Propriedade>();
    public DbSet<AlertaDesmatamento> AlertasDesmatamento => Set<AlertaDesmatamento>();
    public DbSet<LeituraNdvi> LeiturasNdvi => Set<LeituraNdvi>();
    public DbSet<Especie> Especies => Set<Especie>();
    public DbSet<RegistroBiodiversidade> RegistrosBiodiversidade => Set<RegistroBiodiversidade>();
    public DbSet<VersaoCatalogo> VersoesCatalogo => Set<VersaoCatalogo>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<VersaoCatalogo>(e =>
        {
            e.ToTable("catalogo_versao");
            e.HasKey(x => x.Chave);
            e.Property(x => x.Chave).HasColumnName("chave").HasMaxLength(50);
            e.Property(x => x.Hash).HasColumnName("hash").HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<Certificacao>(e =>
        {
            e.ToTable("certificacoes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Sigla).HasMaxLength(5);
            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.Property(x => x.Escopo).HasMaxLength(300);
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.Cor).HasMaxLength(9);
            e.Property(x => x.Organismo).HasMaxLength(200);
            e.OwnsMany(x => x.Categorias, c =>
            {
                c.ToTable("certificacao_categorias");
                c.WithOwner().HasForeignKey("CertificacaoId");
                c.Property<int>("Id");
                c.HasKey("Id");
                c.Property(x => x.Nome).HasMaxLength(200);
            });
        });

        modelBuilder.Entity<Requisito>(e =>
        {
            e.ToTable("requisitos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).HasMaxLength(100);
            e.Property(x => x.Titulo).HasMaxLength(600);
            e.Property(x => x.Descricao).HasMaxLength(1200);
            e.Property(x => x.Tema).HasMaxLength(200);
            e.Property(x => x.Prioridade).HasMaxLength(30);
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.Responsavel).HasMaxLength(200);
            e.Property(x => x.Periodicidade).HasMaxLength(300);
            e.Property(x => x.Observacao).HasMaxLength(1000);
            e.OwnsMany(x => x.Referencias, r =>
            {
                r.ToTable("requisito_referencias");
                r.WithOwner().HasForeignKey("RequisitoId");
                r.Property<int>("Id");
                r.HasKey("Id");
                r.Property(x => x.Norma).HasMaxLength(100);
                r.Property(x => x.Versao).HasMaxLength(100);
                r.Property(x => x.Codigo).HasMaxLength(100);
            });
        });

        modelBuilder.Entity<Vistoria>(e =>
        {
            e.ToTable("vistorias");
            e.HasKey(x => x.Id);
            e.Property(x => x.CodigoDocumento).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.CodigoDocumento).IsUnique();
            e.Property(x => x.UnidadePropriedade).HasMaxLength(200).IsRequired();
            e.Property(x => x.SetorArea).HasMaxLength(200);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.TecnicoResponsavel).HasMaxLength(200);
            e.Property(x => x.CargoTecnico).HasMaxLength(200);
            e.Property(x => x.Motivo).HasMaxLength(300);
            e.Property(x => x.AprovadoPor).HasMaxLength(200);
            e.Property(x => x.CargoAprovador).HasMaxLength(200);
            e.Property(x => x.UsuarioId).HasMaxLength(100);
            e.Property(x => x.UsuarioNome).HasMaxLength(200);
            e.HasMany(x => x.Apontamentos)
                .WithOne(a => a.Vistoria)
                .HasForeignKey(a => a.VistoriaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Apontamento>(e =>
        {
            e.ToTable("apontamentos_vistoria");
            e.HasKey(x => x.Id);
            e.Property(x => x.Anomalia).HasMaxLength(500).IsRequired();
            e.Property(x => x.Localizacao).HasMaxLength(300);
            e.Property(x => x.GrauRisco).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ResponsavelExecucao).HasMaxLength(200);
            e.Property(x => x.SituacaoEncontrada).HasMaxLength(2000);
            e.Property(x => x.SituacaoRequerida).HasMaxLength(2000);
            e.Property(x => x.RiscoAssociado).HasMaxLength(2000);
            e.Property(x => x.ObservacoesEncerramento).HasMaxLength(2000);
            e.Property(x => x.ImagemEncontradaUrl).HasMaxLength(500);
            e.Property(x => x.ImagemRequeridaUrl).HasMaxLength(500);
            e.Property(x => x.ImagemEvidenciaEncerramentoUrl).HasMaxLength(500);
            // Excluir um requisito do catálogo não apaga o histórico da vistoria: só desfaz o vínculo.
            e.HasOne(x => x.Requisito).WithMany().HasForeignKey(x => x.RequisitoId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<Propriedade>(e =>
        {
            e.ToTable("propriedades");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.Property(x => x.CarCodigo).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.CarCodigo).IsUnique();
            e.Property(x => x.Municipio).HasMaxLength(150);
            e.Property(x => x.Uf).HasMaxLength(2);
            e.Property(x => x.FonteGeometria).HasMaxLength(20);
            e.HasMany(x => x.Alertas).WithOne(a => a.Propriedade).HasForeignKey(a => a.PropriedadeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.LeiturasNdvi).WithOne(l => l.Propriedade).HasForeignKey(l => l.PropriedadeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AlertaDesmatamento>(e =>
        {
            e.ToTable("alertas_desmatamento");
            e.HasKey(x => x.Id);
            e.Property(x => x.CodigoAlerta).HasMaxLength(100);
            e.Property(x => x.Fonte).HasMaxLength(100);
            e.Property(x => x.ClasseDesmatamento).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.ImagemAntesUrl).HasMaxLength(1000);
            e.Property(x => x.ImagemDepoisUrl).HasMaxLength(1000);
            e.Property(x => x.Observacoes).HasMaxLength(2000);
            e.HasIndex(x => new { x.PropriedadeId, x.CodigoAlerta, x.Fonte });
        });

        modelBuilder.Entity<LeituraNdvi>(e =>
        {
            e.ToTable("leituras_ndvi");
            e.HasKey(x => x.Id);
            e.Property(x => x.ImagemCorRealUrl).HasMaxLength(500);
            e.Property(x => x.ImagemMapaCalorUrl).HasMaxLength(500);
            e.HasIndex(x => new { x.PropriedadeId, x.DataImagem }).IsUnique();
        });

        modelBuilder.Entity<Especie>(e =>
        {
            e.ToTable("especies");
            e.HasKey(x => x.Id);
            e.Property(x => x.NomePopular).HasMaxLength(200).IsRequired();
            e.Property(x => x.NomeCientifico).HasMaxLength(200);
            e.Property(x => x.Reino).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Grupo).HasMaxLength(50);
            e.Property(x => x.StatusConservacao).HasMaxLength(5);
            e.Property(x => x.Descricao).HasMaxLength(2000);
            e.Property(x => x.FotoUrl).HasMaxLength(500);
            e.HasMany(x => x.Registros).WithOne(r => r.Especie).HasForeignKey(r => r.EspecieId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RegistroBiodiversidade>(e =>
        {
            e.ToTable("registros_biodiversidade");
            e.HasKey(x => x.Id);
            e.Property(x => x.Origem).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Camera).HasMaxLength(150);
            e.Property(x => x.Area).HasMaxLength(60);
            e.Property(x => x.Observacao).HasMaxLength(2000);
            e.Property(x => x.FotoUrl).HasMaxLength(500);
            e.Property(x => x.RegistradoPor).HasMaxLength(200);
            e.HasOne(x => x.Propriedade).WithMany().HasForeignKey(x => x.PropriedadeId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.DataHora);
        });
    }
}
