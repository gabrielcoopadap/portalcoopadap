using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgroCert.Application.Util;
using AgroCert.Domain.Entidades;
using AgroCert.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgroCert.Infrastructure.Persistencia.Seed;

public class InicializadorBanco
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AgroCertDbContext _db;
    private readonly ILogger<InicializadorBanco> _log;

    public InicializadorBanco(AgroCertDbContext db, ILogger<InicializadorBanco> log)
    {
        _db = db;
        _log = log;
    }

    public async Task ExecutarAsync(bool aplicarMigracoes, bool carregarDados, CancellationToken ct = default)
    {
        if (aplicarMigracoes)
        {
            if (!_db.Database.GetMigrations().Any())
                throw new InvalidOperationException(
                    "Nenhuma migration encontrada. Rode banco/gerar-migracao-inicial.ps1 (veja o LEIA-ME).");
            await _db.Database.MigrateAsync(ct);
        }
        if (!carregarDados) return;

        await SemearCertificacoesAsync(ct);
        await SemearRequisitosAsync(ct);
        await SemearFaunaFloraAsync(ct);
    }

    private async Task SemearCertificacoesAsync(CancellationToken ct)
    {
        if (await _db.Certificacoes.AnyAsync(ct)) return;

        _db.Certificacoes.AddRange(
            Base("RA", "Rainforest Alliance", "Produção de café — 420 ha", "Conforme", "#1f8a4c", 87, 156, 136, 12, 8, "Bureau Veritas",
                new(2025, 3, 13), new(2024, 3, 19), new(2025, 3, 14),
                ("Gestão Ambiental", 92, 48), ("Condições de Trabalho", 88, 38), ("Meios de Vida", 85, 32),
                ("Biodiversidade", 79, 22), ("Gestão da Fazenda", 91, 16)),
            Base("GG", "GlobalG.A.P.", "Café, Citrus — 850 ha", "Em Andamento", "#1f6fb2", 74, 214, 158, 34, 22, "SGS",
                new(2025, 6, 8), new(2024, 6, 9), new(2025, 6, 9),
                ("Segurança do Alimento", 62, 40), ("Rastreabilidade", 48, 30), ("Uso de Agroquímicos", 55, 44), ("Meio Ambiente", 51, 38)),
            Base("SM", "SMETA", "Toda a operação cooperativa", "Pendente", "#7c3aed", 67, 98, 66, 20, 12, "Intertek",
                new(2025, 9, 20), new(2024, 9, 21), new(2025, 9, 21),
                ("Trabalho", 30, 26), ("Saúde e Segurança", 22, 24), ("Meio Ambiente", 8, 20), ("Ética Empresarial", 6, 28)),
            Base("CP", "CAFE Practices", "Café Arábica — 320 ha", "Conforme", "#8a4b1f", 91, 120, 109, 8, 3, "SCS Global Services",
                new(2025, 11, 3), new(2024, 11, 4), new(2025, 11, 4),
                ("Qualidade", 40, 30), ("Social", 32, 28), ("Ambiental", 22, 34), ("Econômico", 15, 28)));

        await _db.SaveChangesAsync(ct);
    }

    private static Certificacao Base(string sigla, string nome, string escopo, string status, string cor,
        int conf, int total, int conformes, int pend, int nc, string org,
        DateOnly valido, DateOnly ultima, DateOnly proxima, params (string Nome, int Conformes, int Total)[] categorias) => new()
    {
        Sigla = sigla, Nome = nome, Escopo = escopo, Status = status, Cor = cor,
        Conformidade = conf, TotalRequisitos = total, Conformes = conformes, Pendentes = pend, NaoConformes = nc,
        Organismo = org, ValidoAte = valido, UltimaAuditoria = ultima, ProximaAuditoria = proxima,
        Categorias = categorias.Select(c => new CategoriaConformidade { Nome = c.Nome, Conformes = c.Conformes, Total = c.Total }).ToList(),
    };

    private const string ArquivoRequisitos = "requisitos.json";

    private async Task SemearRequisitosAsync(CancellationToken ct)
    {
        try
        {
            byte[] conteudo;
            await using (var stream = AbrirRecurso(ArquivoRequisitos))
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms, ct);
                conteudo = ms.ToArray();
            }
            var hash = Convert.ToHexString(SHA256.HashData(conteudo));

            var atual = await LerVersaoAsync("requisitos", ct);
            var formato = await LerVersaoAsync("requisitos-formato", ct);

            var novos = (JsonSerializer.Deserialize<List<RequisitoArquivo>>(conteudo, Json) ?? new()).Select(a => a.ParaEntidade()).ToList();

            if (atual == hash)
            {
                if (formato == FormatoRequisitos) return;
                var existentes = await _db.Requisitos.ToListAsync(ct);
                var preenchidos = 0;
                foreach (var r in existentes)
                {
                    var n = novos.FirstOrDefault(x => x.Titulo == r.Titulo);
                    if (n is null) continue;
                    r.OQueE = n.OQueE; r.PassoAPasso = n.PassoAPasso; r.Evidencias = n.Evidencias;
                    r.Autoverificacao = n.Autoverificacao; r.ErrosComuns = n.ErrosComuns; r.OndeBuscarAjuda = n.OndeBuscarAjuda;
                    preenchidos++;
                }
                await _db.SaveChangesAsync(ct);
                await GravarVersaoAsync("requisitos-formato", FormatoRequisitos, ct);
                _log.LogInformation("Requisitos: guia \"como cumprir\" preenchido em {Total} requisito(s).", preenchidos);
                return;
            }

            await using var transacao = await _db.Database.BeginTransactionAsync(ct);

            var antigos = await _db.Requisitos.ToListAsync(ct);
            var equivalente = new Dictionary<long, Requisito>();
            foreach (var antigo in antigos)
            {
                var melhor = MaisParecido(antigo, novos);
                if (melhor is null) continue;
                equivalente[antigo.Id] = melhor;
                if (string.IsNullOrWhiteSpace(melhor.Responsavel) && !string.IsNullOrWhiteSpace(antigo.Responsavel)) melhor.Responsavel = antigo.Responsavel;
                if (melhor.Prazo is null && antigo.Prazo is not null) melhor.Prazo = antigo.Prazo;
                // O andamento marcado no portal vale mais que o status padrão do arquivo.
                if (!string.IsNullOrWhiteSpace(antigo.Status) && antigo.Status != "Pendente") melhor.Status = antigo.Status;
            }

            _db.Requisitos.AddRange(novos);
            await _db.SaveChangesAsync(ct);

            var apontamentos = await _db.Apontamentos.Where(a => a.RequisitoId != null).ToListAsync(ct);
            foreach (var a in apontamentos)
            {
                if (a.RequisitoId is long idAntigo && antigos.Any(r => r.Id == idAntigo))
                    a.RequisitoId = equivalente.TryGetValue(idAntigo, out var novo) ? novo.Id : null;
            }

            _db.Requisitos.RemoveRange(antigos);
            await _db.SaveChangesAsync(ct);

            await GravarVersaoAsync("requisitos", hash, ct);
            await GravarVersaoAsync("requisitos-formato", FormatoRequisitos, ct);

            await transacao.CommitAsync(ct);
            _log.LogInformation("Catálogo de requisitos atualizado: {Antigos} antigo(s) substituído(s) por {Novos} requisito(s) de {Arquivo}.",
                antigos.Count, novos.Count, ArquivoRequisitos);
        }
        catch (Exception e)
        {
            _db.ChangeTracker.Clear();
            _log.LogError(e, "Falha ao carregar {Arquivo} — o catálogo anterior foi mantido.", ArquivoRequisitos);
        }
    }

    private const string FormatoRequisitos = "2";

    private async Task<string?> LerVersaoAsync(string chave, CancellationToken ct) =>
        (await _db.VersoesCatalogo.AsNoTracking().FirstOrDefaultAsync(v => v.Chave == chave, ct))?.Hash;

    private async Task GravarVersaoAsync(string chave, string valor, CancellationToken ct)
    {
        var versao = await _db.VersoesCatalogo.FirstOrDefaultAsync(v => v.Chave == chave, ct);
        if (versao is null) _db.VersoesCatalogo.Add(new VersaoCatalogo { Chave = chave, Hash = valor });
        else versao.Hash = valor;
        await _db.SaveChangesAsync(ct);
    }

    private sealed class RequisitoArquivo
    {
        public string? Codigo { get; set; }
        public string? Titulo { get; set; }
        public string? Descricao { get; set; }
        public string? Tema { get; set; }
        public string? Prioridade { get; set; }
        public string? Status { get; set; }
        public string? Responsavel { get; set; }
        public string? Periodicidade { get; set; }
        public string? Observacao { get; set; }
        public List<Referencia> Referencias { get; set; } = new();
        [JsonPropertyName("o_que_e")] public string? OQueE { get; set; }
        [JsonPropertyName("passo_a_passo")] public List<string>? PassoAPasso { get; set; }
        [JsonPropertyName("evidencias")] public List<string>? Evidencias { get; set; }
        [JsonPropertyName("autoverificacao")] public List<string>? Autoverificacao { get; set; }
        [JsonPropertyName("erros_comuns")] public List<string>? ErrosComuns { get; set; }
        [JsonPropertyName("onde_buscar_ajuda")] public List<string>? OndeBuscarAjuda { get; set; }

        private static string? Juntar(List<string>? l) =>
            l is null || l.Count == 0 ? null : string.Join("\n", l.Select(x => x.Replace("\r", " ").Replace("\n", " ").Trim()).Where(x => x.Length > 0));

        public Requisito ParaEntidade() => new()
        {
            Codigo = Codigo, Titulo = Titulo, Descricao = Descricao, Tema = Tema, Prioridade = Prioridade, Status = Status,
            Responsavel = string.IsNullOrEmpty(Responsavel) ? null : Responsavel,
            Periodicidade = string.IsNullOrEmpty(Periodicidade) ? null : Periodicidade,
            Observacao = Observacao, Referencias = Referencias,
            OQueE = string.IsNullOrWhiteSpace(OQueE) ? null : OQueE.Trim(),
            PassoAPasso = Juntar(PassoAPasso), Evidencias = Juntar(Evidencias), Autoverificacao = Juntar(Autoverificacao),
            ErrosComuns = Juntar(ErrosComuns), OndeBuscarAjuda = Juntar(OndeBuscarAjuda),
        };
    }

    private static Requisito? MaisParecido(Requisito antigo, List<Requisito> novos)
    {
        var mesmoTitulo = novos.FirstOrDefault(n => antigo.Titulo != null && n.Titulo == antigo.Titulo);
        if (mesmoTitulo != null) return mesmoTitulo;

        var refsAntigo = new HashSet<string>(antigo.Referencias.Select(x => Chave(x.Norma, x.Codigo)));
        Requisito? melhor = null;
        var maior = 0;
        foreach (var novo in novos)
        {
            var comum = novo.Referencias.Count(x => refsAntigo.Contains(Chave(x.Norma, x.Codigo)));
            if (comum > maior) { maior = comum; melhor = novo; }
        }
        return melhor;
    }

    private static readonly (string Popular, string Cientifico, Reino Reino, string Grupo, string Status, int Peso)[] EspeciesExemplo =
    {
        ("Onça-parda", "Puma concolor", Reino.FAUNA, "Mamífero", "VU", 6),
        ("Lobo-guará", "Chrysocyon brachyurus", Reino.FAUNA, "Mamífero", "VU", 8),
        ("Tamanduá-bandeira", "Myrmecophaga tridactyla", Reino.FAUNA, "Mamífero", "VU", 7),
        ("Anta", "Tapirus terrestris", Reino.FAUNA, "Mamífero", "VU", 6),
        ("Veado-catingueiro", "Mazama gouazoubira", Reino.FAUNA, "Mamífero", "LC", 14),
        ("Tatu-galinha", "Dasypus novemcinctus", Reino.FAUNA, "Mamífero", "LC", 13),
        ("Capivara", "Hydrochoerus hydrochaeris", Reino.FAUNA, "Mamífero", "LC", 12),
        ("Tucano-toco", "Ramphastos toco", Reino.FAUNA, "Ave", "LC", 18),
        ("Seriema", "Cariama cristata", Reino.FAUNA, "Ave", "LC", 16),
        ("Gralha-do-campo", "Cyanocorax cristatellus", Reino.FAUNA, "Ave", "LC", 15),
        ("Arara-canindé", "Ara ararauna", Reino.FAUNA, "Ave", "LC", 11),
        ("Ema", "Rhea americana", Reino.FAUNA, "Ave", "NT", 7),
        ("Curicaca", "Theristicus caudatus", Reino.FAUNA, "Ave", "LC", 9),
        ("Jiboia", "Boa constrictor", Reino.FAUNA, "Réptil", "LC", 4),
        ("Teiú", "Salvator merianae", Reino.FAUNA, "Réptil", "LC", 8),
        ("Cascavel", "Crotalus durissus", Reino.FAUNA, "Réptil", "LC", 3),
        ("Sapo-cururu", "Rhinella diptycha", Reino.FAUNA, "Anfíbio", "LC", 6),
        ("Rã-manteiga", "Leptodactylus latrans", Reino.FAUNA, "Anfíbio", "LC", 4),
        ("Abelha-jataí", "Tetragonisca angustula", Reino.FAUNA, "Inseto", "LC", 9),
        ("Borboleta-azul", "Morpho helenor", Reino.FAUNA, "Inseto", "LC", 5),
        ("Ipê-amarelo-do-cerrado", "Handroanthus ochraceus", Reino.FLORA, "Árvore", "LC", 12),
        ("Pequizeiro", "Caryocar brasiliense", Reino.FLORA, "Árvore", "LC", 11),
        ("Barbatimão", "Stryphnodendron adstringens", Reino.FLORA, "Árvore", "LC", 7),
        ("Aroeira", "Myracrodruon urundeuva", Reino.FLORA, "Árvore", "LC", 6),
        ("Jatobá-do-cerrado", "Hymenaea stigonocarpa", Reino.FLORA, "Árvore", "LC", 6),
        ("Mangabeira", "Hancornia speciosa", Reino.FLORA, "Árvore", "LC", 5),
        ("Buriti", "Mauritia flexuosa", Reino.FLORA, "Palmeira", "LC", 9),
        ("Macaúba", "Acrocomia aculeata", Reino.FLORA, "Palmeira", "LC", 6),
        ("Lobeira", "Solanum lycocarpum", Reino.FLORA, "Arbusto", "LC", 7),
        ("Canela-de-ema", "Vellozia squamata", Reino.FLORA, "Arbusto", "LC", 4),
        ("Capim-dourado", "Syngonanthus nitens", Reino.FLORA, "Herbácea", "LC", 5),
    };

    private async Task SemearFaunaFloraAsync(CancellationToken ct)
    {
        if (await _db.Especies.AnyAsync(ct)) return;

        var propriedade = (await _db.Propriedades.ToListAsync(ct)).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.GeoJson));
        if (propriedade is null)
        {
            string Poligono(params (double X, double Y)[] pts) =>
                GeoUtil.EscreverMultiPolygon(new[] { new GeoUtil.Poligono(new List<(double X, double Y)[]> { pts }) });
            propriedade = new Propriedade
            {
                Nome = "Fazenda Demonstração (dados de exemplo)",
                CarCodigo = "MG-3155801-DEMONSTRACAO",
                Municipio = "Rio Paranaíba",
                Uf = "MG",
                FonteGeometria = "MANUAL",
                GeoJson = Poligono((-46.262, -19.192), (-46.240, -19.188), (-46.226, -19.196), (-46.224, -19.212), (-46.236, -19.222), (-46.255, -19.220), (-46.265, -19.207), (-46.262, -19.192)),
                GeoJsonReservaLegal = Poligono((-46.247, -19.192), (-46.232, -19.194), (-46.229, -19.205), (-46.240, -19.209), (-46.250, -19.202), (-46.247, -19.192)),
                GeoJsonApp = Poligono((-46.262, -19.214), (-46.250, -19.207), (-46.238, -19.203), (-46.228, -19.199), (-46.227, -19.2025), (-46.237, -19.2068), (-46.249, -19.2108), (-46.261, -19.2172), (-46.262, -19.214)),
            };
            propriedade.AreaHa = GeoUtil.AreaHectares(propriedade.GeoJson);
            propriedade.AreaReservaLegalHa = GeoUtil.AreaHectares(propriedade.GeoJsonReservaLegal);
            propriedade.AreaAppHa = GeoUtil.AreaHectares(propriedade.GeoJsonApp);
            _db.Propriedades.Add(propriedade);
        }

        var car = GeoUtil.LerGeoJson(propriedade.GeoJson!);
        var rl = string.IsNullOrWhiteSpace(propriedade.GeoJsonReservaLegal) ? new List<GeoUtil.Poligono>() : GeoUtil.LerGeoJson(propriedade.GeoJsonReservaLegal);
        var app = string.IsNullOrWhiteSpace(propriedade.GeoJsonApp) ? new List<GeoUtil.Poligono>() : GeoUtil.LerGeoJson(propriedade.GeoJsonApp);
        var bbox = GeoUtil.BoundingBox(car)!;
        bool Dentro(List<GeoUtil.Poligono> ps, (double, double) pt) => ps.Any(p => GeoUtil.Contem(p, pt));

        var rnd = new Random(2026);
        var cameras = new[] { "Câmera 01 — Reserva Legal", "Câmera 03 — APP Córrego", "Câmera 05 — Reserva Legal" };
        var agora = DateTime.Now;

        foreach (var (popular, cientifico, reino, grupo, status, peso) in EspeciesExemplo)
        {
            var especie = new Especie
            {
                NomePopular = popular, NomeCientifico = cientifico, Reino = reino, Grupo = grupo, StatusConservacao = status,
                Descricao = reino == Reino.FAUNA ? $"{grupo} nativo do Cerrado, registrado nas áreas da propriedade." : $"Espécie nativa do Cerrado ({grupo.ToLowerInvariant()}).",
            };
            _db.Especies.Add(especie);

            var quantidade = peso * 2 + rnd.Next(0, 5);
            for (var i = 0; i < quantidade; i++)
            {
                var diasAtras = (int)(730 * Math.Pow(rnd.NextDouble(), 1.3));
                var data = agora.Date.AddDays(-diasAtras).AddHours(5 + rnd.Next(0, 15)).AddMinutes(rnd.Next(0, 60));
                if (data > agora) data = agora.AddMinutes(-rnd.Next(10, 600));

                (double X, double Y) pt;
                var tentativas = 0;
                do
                {
                    var preferir = rnd.NextDouble() < 0.6 ? (rnd.NextDouble() < 0.6 ? rl : app) : car;
                    var b = GeoUtil.BoundingBox(preferir.Count > 0 ? preferir : car)!;
                    pt = (b[0] + rnd.NextDouble() * (b[2] - b[0]), b[1] + rnd.NextDouble() * (b[3] - b[1]));
                } while (!Dentro(car, pt) && ++tentativas < 50);

                var area = Dentro(rl, pt) ? "Reserva Legal" : Dentro(app, pt) ? "APP" : rnd.NextDouble() < 0.6 ? "Área de conservação" : "Lavoura";
                var sorteio = rnd.NextDouble();
                var origem = reino == Reino.FLORA
                    ? (sorteio < 0.7 ? OrigemRegistro.COLETA : OrigemRegistro.AVISTAMENTO)
                    : grupo == "Mamífero" && sorteio < 0.45 ? OrigemRegistro.CAMERA
                    : sorteio < 0.8 ? OrigemRegistro.AVISTAMENTO : OrigemRegistro.VESTIGIO;

                _db.RegistrosBiodiversidade.Add(new RegistroBiodiversidade
                {
                    Especie = especie,
                    Propriedade = propriedade,
                    DataHora = data,
                    Origem = origem,
                    Camera = origem == OrigemRegistro.CAMERA ? cameras[rnd.Next(cameras.Length)] : null,
                    Latitude = Math.Round(pt.Y, 6),
                    Longitude = Math.Round(pt.X, 6),
                    Area = area,
                    Quantidade = reino == Reino.FAUNA && rnd.NextDouble() < 0.2 ? rnd.Next(2, 5) : 1,
                    RegistradoPor = origem == OrigemRegistro.CAMERA ? "Armadilha fotográfica" : "Equipe de campo",
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Fauna e Flora: {Especies} espécies e registros de exemplo carregados.", EspeciesExemplo.Length);
    }

    private static string Chave(string? norma, string? codigo) => (norma ?? "") + "::" + (codigo ?? "");

    private static Stream AbrirRecurso(string nome) =>
        typeof(InicializadorBanco).Assembly.GetManifestResourceStream(nome)
        ?? throw new FileNotFoundException("Recurso embutido não encontrado: " + nome);
}
