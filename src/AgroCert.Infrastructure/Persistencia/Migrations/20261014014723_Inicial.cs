using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AgroCert.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "certificacao");

            migrationBuilder.CreateTable(
                name: "catalogo_versao",
                schema: "certificacao",
                columns: table => new
                {
                    chave = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogo_versao", x => x.chave);
                });

            migrationBuilder.CreateTable(
                name: "certificacoes",
                schema: "certificacao",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Sigla = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Escopo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Cor = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    Conformidade = table.Column<int>(type: "integer", nullable: true),
                    TotalRequisitos = table.Column<int>(type: "integer", nullable: true),
                    Conformes = table.Column<int>(type: "integer", nullable: true),
                    Pendentes = table.Column<int>(type: "integer", nullable: true),
                    NaoConformes = table.Column<int>(type: "integer", nullable: true),
                    Organismo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ValidoAte = table.Column<DateOnly>(type: "date", nullable: true),
                    UltimaAuditoria = table.Column<DateOnly>(type: "date", nullable: true),
                    ProximaAuditoria = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificacoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "especies",
                schema: "certificacao",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NomePopular = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NomeCientifico = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Reino = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Grupo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StatusConservacao = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    Descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_especies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "propriedades",
                schema: "certificacao",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CarCodigo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AreaHa = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    AreaReservaLegalHa = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    AreaAppHa = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    AreaVegetacaoNativaHa = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Municipio = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    GeoJson = table.Column<string>(type: "text", nullable: true),
                    GeoJsonReservaLegal = table.Column<string>(type: "text", nullable: true),
                    GeoJsonApp = table.Column<string>(type: "text", nullable: true),
                    FonteGeometria = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_propriedades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "requisitos",
                schema: "certificacao",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Titulo = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    Descricao = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    Tema = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Prioridade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Responsavel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Periodicidade = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Prazo = table.Column<DateOnly>(type: "date", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OQueE = table.Column<string>(type: "text", nullable: true),
                    PassoAPasso = table.Column<string>(type: "text", nullable: true),
                    Evidencias = table.Column<string>(type: "text", nullable: true),
                    Autoverificacao = table.Column<string>(type: "text", nullable: true),
                    ErrosComuns = table.Column<string>(type: "text", nullable: true),
                    OndeBuscarAjuda = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requisitos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vistorias",
                schema: "certificacao",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CodigoDocumento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UnidadePropriedade = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SetorArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DataVistoria = table.Column<DateOnly>(type: "date", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TecnicoResponsavel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CargoTecnico = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AprovadoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CargoAprovador = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UsuarioId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UsuarioNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vistorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "certificacao_categorias",
                schema: "certificacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Conformes = table.Column<int>(type: "integer", nullable: true),
                    Total = table.Column<int>(type: "integer", nullable: true),
                    CertificacaoId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificacao_categorias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_certificacao_categorias_certificacoes_CertificacaoId",
                        column: x => x.CertificacaoId,
                        principalSchema: "certificacao",
                        principalTable: "certificacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alertas_desmatamento",
                schema: "certificacao",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropriedadeId = table.Column<long>(type: "bigint", nullable: false),
                    CodigoAlerta = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Fonte = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DataDeteccao = table.Column<DateOnly>(type: "date", nullable: true),
                    AreaHa = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ClasseDesmatamento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DentroReservaLegal = table.Column<bool>(type: "boolean", nullable: true),
                    DentroApp = table.Column<bool>(type: "boolean", nullable: true),
                    GeoJson = table.Column<string>(type: "text", nullable: true),
                    ImagemAntesUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ImagemDepoisUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alertas_desmatamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alertas_desmatamento_propriedades_PropriedadeId",
                        column: x => x.PropriedadeId,
                        principalSchema: "certificacao",
                        principalTable: "propriedades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "leituras_ndvi",
                schema: "certificacao",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropriedadeId = table.Column<long>(type: "bigint", nullable: false),
                    DataImagem = table.Column<DateOnly>(type: "date", nullable: false),
                    NdviMedio = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    NdviMinimo = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    NdviMaximo = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    PercentualAmostrasValidas = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Amostras = table.Column<int>(type: "integer", nullable: true),
                    ImagemCorRealUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImagemMapaCalorUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leituras_ndvi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leituras_ndvi_propriedades_PropriedadeId",
                        column: x => x.PropriedadeId,
                        principalSchema: "certificacao",
                        principalTable: "propriedades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registros_biodiversidade",
                schema: "certificacao",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EspecieId = table.Column<long>(type: "bigint", nullable: false),
                    PropriedadeId = table.Column<long>(type: "bigint", nullable: true),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Camera = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Area = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegistradoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Confirmado = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_biodiversidade", x => x.Id);
                    table.ForeignKey(
                        name: "FK_registros_biodiversidade_especies_EspecieId",
                        column: x => x.EspecieId,
                        principalSchema: "certificacao",
                        principalTable: "especies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registros_biodiversidade_propriedades_PropriedadeId",
                        column: x => x.PropriedadeId,
                        principalSchema: "certificacao",
                        principalTable: "propriedades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "requisito_referencias",
                schema: "certificacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Norma = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Versao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Codigo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequisitoId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requisito_referencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_requisito_referencias_requisitos_RequisitoId",
                        column: x => x.RequisitoId,
                        principalSchema: "certificacao",
                        principalTable: "requisitos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "apontamentos_vistoria",
                schema: "certificacao",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VistoriaId = table.Column<long>(type: "bigint", nullable: false),
                    RequisitoId = table.Column<long>(type: "bigint", nullable: true),
                    Anomalia = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Localizacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    GrauRisco = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Prazo = table.Column<DateOnly>(type: "date", nullable: true),
                    ResponsavelExecucao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SituacaoEncontrada = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SituacaoRequerida = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RiscoAssociado = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ImagemEncontradaUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImagemRequeridaUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataEncerramento = table.Column<DateOnly>(type: "date", nullable: true),
                    ImagemEvidenciaEncerramentoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ObservacoesEncerramento = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_apontamentos_vistoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_apontamentos_vistoria_requisitos_RequisitoId",
                        column: x => x.RequisitoId,
                        principalSchema: "certificacao",
                        principalTable: "requisitos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_apontamentos_vistoria_vistorias_VistoriaId",
                        column: x => x.VistoriaId,
                        principalSchema: "certificacao",
                        principalTable: "vistorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alertas_desmatamento_PropriedadeId_CodigoAlerta_Fonte",
                schema: "certificacao",
                table: "alertas_desmatamento",
                columns: new[] { "PropriedadeId", "CodigoAlerta", "Fonte" });

            migrationBuilder.CreateIndex(
                name: "IX_apontamentos_vistoria_RequisitoId",
                schema: "certificacao",
                table: "apontamentos_vistoria",
                column: "RequisitoId");

            migrationBuilder.CreateIndex(
                name: "IX_apontamentos_vistoria_Status",
                schema: "certificacao",
                table: "apontamentos_vistoria",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_apontamentos_vistoria_VistoriaId",
                schema: "certificacao",
                table: "apontamentos_vistoria",
                column: "VistoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_certificacao_categorias_CertificacaoId",
                schema: "certificacao",
                table: "certificacao_categorias",
                column: "CertificacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_leituras_ndvi_PropriedadeId_DataImagem",
                schema: "certificacao",
                table: "leituras_ndvi",
                columns: new[] { "PropriedadeId", "DataImagem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_propriedades_CarCodigo",
                schema: "certificacao",
                table: "propriedades",
                column: "CarCodigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registros_biodiversidade_DataHora",
                schema: "certificacao",
                table: "registros_biodiversidade",
                column: "DataHora");

            migrationBuilder.CreateIndex(
                name: "IX_registros_biodiversidade_EspecieId",
                schema: "certificacao",
                table: "registros_biodiversidade",
                column: "EspecieId");

            migrationBuilder.CreateIndex(
                name: "IX_registros_biodiversidade_PropriedadeId",
                schema: "certificacao",
                table: "registros_biodiversidade",
                column: "PropriedadeId");

            migrationBuilder.CreateIndex(
                name: "IX_requisito_referencias_RequisitoId",
                schema: "certificacao",
                table: "requisito_referencias",
                column: "RequisitoId");

            migrationBuilder.CreateIndex(
                name: "IX_vistorias_CodigoDocumento",
                schema: "certificacao",
                table: "vistorias",
                column: "CodigoDocumento",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alertas_desmatamento",
                schema: "certificacao");

            migrationBuilder.DropTable(
                name: "apontamentos_vistoria",
                schema: "certificacao");

            migrationBuilder.DropTable(
                name: "catalogo_versao",
                schema: "certificacao");

            migrationBuilder.DropTable(
                name: "certificacao_categorias",
                schema: "certificacao");

            migrationBuilder.DropTable(
                name: "leituras_ndvi",
                schema: "certificacao");

            migrationBuilder.DropTable(
                name: "registros_biodiversidade",
                schema: "certificacao");

            migrationBuilder.DropTable(
                name: "requisito_referencias",
                schema: "certificacao");

            migrationBuilder.DropTable(
                name: "vistorias",
                schema: "certificacao");

            migrationBuilder.DropTable(
                name: "certificacoes",
                schema: "certificacao");

            migrationBuilder.DropTable(
                name: "especies",
                schema: "certificacao");

            migrationBuilder.DropTable(
                name: "propriedades",
                schema: "certificacao");

            migrationBuilder.DropTable(
                name: "requisitos",
                schema: "certificacao");
        }
    }
}
