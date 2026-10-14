using System.Globalization;
using System.Text;
using AgroCert.Application.Dtos;
using AgroCert.Application.Interfaces;
using AgroCert.Application.Util;
using AgroCert.Domain.Excecoes;

namespace AgroCert.Application.Servicos;

public partial class ConformidadeAmbientalServico
{
    public static readonly (string Chave, string Rotulo, int Ano)[] MarcosPadrao =
    {
        ("2008", "Código Florestal / ATITUDES — 22/07/2008", 2008),
        ("2014", "Rainforest Alliance — 01/01/2014", 2014),
        ("2020", "ATITUDES — 31/12/2020", 2020),
    };

    // manchas menores (~0,4 ha) costumam ser erro de classificação
    public const int MinPixels = 5;
    private const int MaxAreas = 40;
    private const int MaxAreasComImagem = 12;
    private const int MaxAreasComSatelite = 6;
    private const double RaioBuscaUcM = 10_000;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly IAmbientalRepositorio _repo;
    private readonly ICoberturaMapBiomas _cobertura;
    private readonly IUnidadesConservacao _ucs;
    private readonly ICopernicusCliente _copernicus;
    private readonly MonitoramentoAmbientalServico _monitoramento;

    public ConformidadeAmbientalServico(IAmbientalRepositorio repo, ICoberturaMapBiomas cobertura, IUnidadesConservacao ucs,
        ICopernicusCliente copernicus, MonitoramentoAmbientalServico monitoramento)
    {
        _repo = repo;
        _cobertura = cobertura;
        _ucs = ucs;
        _copernicus = copernicus;
        _monitoramento = monitoramento;
    }

    public static List<int> AnosSatelite()
    {
        var hoje = DateTime.Today;
        var ultimo = hoje.Month >= 10 ? hoje.Year : hoje.Year - 1;
        return new[] { 2016, 2020, ultimo }.Where(a => a <= ultimo).Distinct().ToList();
    }

    public async Task<RelatorioConformidadeDto> GerarAsync(long propriedadeId, CancellationToken ct = default)
    {
        var p = await _repo.ObterPropriedadeAsync(propriedadeId, ct) ?? throw new NaoEncontradoException("Propriedade não encontrada: " + propriedadeId);
        var ficha = await _monitoramento.GerarFichaAmbientalAsync(propriedadeId, ct);
        var ndvi = await _monitoramento.ListarNdviAsync(propriedadeId, ct);

        var avisos = new List<string>();
        var fontes = new List<string> { "CAR/SICAR (limite do imóvel, Reserva Legal e APP)" };
        var criterios = new List<CriterioConformidade>();
        var anos = _cobertura.Anos.ToList();

        var dto = new RelatorioConformidadeDto
        {
            PropriedadeId = p.Id, Nome = p.Nome, Car = p.CarCodigo, Municipio = p.Municipio, Uf = p.Uf,
            AreaHa = p.AreaHa, AreaReservaLegalHa = p.AreaReservaLegalHa, AreaAppHa = p.AreaAppHa, AreaVegetacaoNativaHa = p.AreaVegetacaoNativaHa,
            GeoJson = p.GeoJson, GeoJsonReservaLegal = p.GeoJsonReservaLegal, GeoJsonApp = p.GeoJsonApp,
            AnosMapBiomas = anos,
            Alertas = ficha.Alertas,
            Ndvi = ndvi,
            CopernicusConfigurado = _copernicus.Configurado,
            AnosSatelite = AnosSatelite(),
            PastaMapBiomas = _cobertura.Pasta,
            PastaUcs = _ucs.Pasta,
            RaioAmortecimentoM = _ucs.RaioAmortecimentoM,
            UcComDados = _ucs.TemDados,
        };

        if (string.IsNullOrWhiteSpace(p.GeoJson))
        {
            avisos.Add("A propriedade não tem o limite do CAR cadastrado — sem ele não é possível comparar os mapas.");
            criterios.AddRange(MarcosPadrao.Select(m => SemDados(m.Chave, "sem o limite do imóvel")));
            return dto with { Criterios = criterios, Avisos = avisos, Fontes = fontes };
        }

        var imovel = GeoUtil.LerGeoJson(p.GeoJson);
        var rl = Ler(p.GeoJsonReservaLegal);
        var app = Ler(p.GeoJsonApp);
        var bb = GeoUtil.BoundingBox(imovel)!;
        var lat0 = (bb[1] + bb[3]) / 2;
        var mx = 111_320 * Math.Cos(lat0 * Math.PI / 180);
        const double my = 110_574;

        var marcos = new List<MarcoTemporal>();
        RelatorioConformidadeDto Resultado(RelatorioConformidadeDto d) => d with { Criterios = criterios, Avisos = avisos, Fontes = fontes, Marcos = marcos };

        AnaliseMapas? analise = null;
        if (anos.Count < 2)
        {
            avisos.Add(anos.Count == 0
                ? $"Nenhum mapa do MapBiomas encontrado em {_cobertura.Pasta}. Coloque lá os arquivos mapbiomas_regiao_<ano>.tif (2008, 2014, 2020 e o mais recente)."
                : $"Só há o mapa de {anos[0]} do MapBiomas — é preciso pelo menos um ano antigo e o mais recente para comparar.");
        }
        else
        {
            var ultimo = anos[^1];
            foreach (var (chave, rotulo, ano) in MarcosPadrao)
            {
                int? usado = anos.Where(a => a <= ano).Select(a => (int?)a).DefaultIfEmpty(null).Max();
                if (usado is null && anos[0] < ultimo) usado = anos[0];
                if (usado >= ultimo) usado = null;
                marcos.Add(new MarcoTemporal(chave, rotulo, ano, usado, usado is not null && usado != ano));
            }
            foreach (var m in marcos.Where(m => m.Aproximado))
                avisos.Add(m.AnoUsado < m.AnoCorte
                    ? $"Falta o mapa de {m.AnoCorte} do MapBiomas: para o marco \"{m.Rotulo}\" foi usado o de {m.AnoUsado} (mais rigoroso: inclui mudanças anteriores ao corte)."
                    : $"Falta o mapa de {m.AnoCorte} do MapBiomas: para o marco \"{m.Rotulo}\" foi usado o de {m.AnoUsado} (mudanças entre {m.AnoCorte} e {m.AnoUsado} não aparecem).");

            var margemM = Math.Max(200, Math.Max((bb[2] - bb[0]) * mx, (bb[3] - bb[1]) * my) * 0.08);
            var caixa = new[] { bb[0] - margemM / mx, bb[1] - margemM / my, bb[2] + margemM / mx, bb[3] + margemM / my };
            analise = AnalisarMapas(anos, marcos, caixa, imovel, rl, app, lat0, avisos);
            if (analise is not null)
                fontes.Add($"MapBiomas — cobertura e uso da terra, pixels de 30 m (mapas de {string.Join(", ", analise.Anos)})");
        }

        var m2008 = marcos.FirstOrDefault(m => m.Chave == "2008");
        var m2014 = marcos.FirstOrDefault(m => m.Chave == "2014");
        var m2020 = marcos.FirstOrDefault(m => m.Chave == "2020");
        if (analise is null)
        {
            criterios.Add(SemDados("2014", "sem os mapas do MapBiomas"));
            criterios.Add(SemDados("2008", "sem os mapas do MapBiomas"));
            criterios.Add(SemDados("2020", "sem os mapas do MapBiomas"));
        }
        else
        {
            var ult = analise.Ultimo;
            decimal Soma(Func<AreaConversao, bool> f) => analise.Areas.Where(f).Sum(a => a.AreaHa);
            int Conta(Func<AreaConversao, bool> f) => analise.Areas.Count(f);
            string Lista(Func<AreaConversao, bool> f) => string.Join(", ", analise.Areas.Where(f).Select(a => a.Numero).Take(12));

            CriterioConformidade Criterio(MarcoTemporal? m, string chave, string norma, string titulo, string exigencia, StatusCriterio seHouver, string acao)
            {
                if (m?.AnoUsado is not int ano) return SemDados(chave, $"falta um mapa anterior a {ult}");
                bool f(AreaConversao a) => a.AnosNatural?.Contains(ano) == true;
                var ha = Soma(f);
                var anterior = m.AnoUsado < m.AnoCorte;
                if (ha <= 0)
                    return new CriterioConformidade(norma, titulo, exigencia, StatusCriterio.Conforme,
                        $"Nenhuma conversão de vegetação nativa entre {ano} e {ult} dentro do imóvel.",
                        m.Aproximado ? $"Mapa de {ano} usado no lugar do de {m.AnoCorte}." : null);
                if (anterior)
                    return new CriterioConformidade(norma, titulo, exigencia, StatusCriterio.Verificar,
                        $"{Fmt(ha)} ha em {Conta(f)} área(s) eram vegetação nativa em {ano} e em {ult} têm uso agropecuário (áreas {Lista(f)}). Sem o mapa de {m.AnoCorte} não dá para saber se a mudança foi antes ou depois de {m.AnoCorte}.",
                        $"Coloque o mapa de {m.AnoCorte} do MapBiomas na pasta para fechar este critério.");
                return new CriterioConformidade(norma, titulo, exigencia, seHouver,
                    $"{Fmt(ha)} ha em {Conta(f)} área(s) eram vegetação nativa em {ano} e em {ult} têm uso agropecuário (áreas {Lista(f)}). {acao}",
                    m.Aproximado ? $"Mapa de {ano} usado no lugar do de {m.AnoCorte}." : null);
            }

            criterios.Add(Criterio(m2014, "2014", "Rainforest Alliance", "Sem conversão de ecossistemas naturais desde 01/01/2014",
                "Nenhuma área de vegetação nativa convertida em uso agropecuário após 01/01/2014.", StatusCriterio.IndicioNaoConformidade, "Confira em campo."));
            criterios.Add(Criterio(m2008, "2008", "ATITUDES / Código Florestal", "Supressão após 22/07/2008 só com autorização",
                "Toda supressão de vegetação nativa depois de 22/07/2008 precisa de autorização do órgão ambiental (ASV/DAIA).", StatusCriterio.Verificar,
                "Apresente a autorização de supressão de cada área ou o termo de regularização."));
            criterios.Add(Criterio(m2020, "2020", "ATITUDES", "Nenhuma supressão após 31/12/2020",
                "Depois de 31/12/2020 não é aceita supressão de vegetação nativa, mesmo com autorização.", StatusCriterio.IndicioNaoConformidade, "Confira em campo."));
            if (analise.ManchasPequenas > 0)
                avisos.Add($"{analise.ManchasPequenas} mancha(s) com menos de 0,4 ha ({Fmt(analise.ManchasPequenasHa)} ha no total) não entraram no parecer nem aparecem no mapa: nesse tamanho costumam ser erro de classificação do mapa.");
        }

        var ucs = new List<UcProxima>();
        string? ucsGeo = null;
        if (!_ucs.TemDados)
        {
            avisos.Add($"Nenhuma camada de unidades de conservação encontrada em {_ucs.Pasta}. Baixe o shapefile do CNUC (MMA) e coloque nessa pasta (pode ser o .zip).");
            criterios.Add(new CriterioConformidade("Rainforest Alliance / ATITUDES", "Fora de unidade de conservação", "Não produzir dentro de UC sem autorização.", StatusCriterio.SemDados, "Camada de unidades de conservação não carregada."));
            criterios.Add(new CriterioConformidade("Rainforest Alliance / ATITUDES", "Fora de zona de amortecimento", "Não produzir em zona de amortecimento de UC sem autorização.", StatusCriterio.SemDados, "Camada de unidades de conservação não carregada."));
        }
        else
        {
            fontes.Add("Unidades de conservação: " + string.Join("; ", _ucs.Fontes));
            avisos.AddRange(_ucs.Avisos);
            (ucs, ucsGeo) = AnalisarUcs(imovel, bb, lat0, mx, my, analise);
            var dentroPi = ucs.Where(u => u.Dentro && !u.EhZonaAmortecimento && u.Grupo == UnidadeConservacao.ProtecaoIntegral).ToList();
            var dentroUs = ucs.Where(u => u.Dentro && !u.EhZonaAmortecimento && u.Grupo != UnidadeConservacao.ProtecaoIntegral).ToList();
            var za = ucs.Where(u => u.Status == StatusCriterio.Verificar && (u.EhZonaAmortecimento || !u.Dentro)).ToList();

            criterios.Add(new CriterioConformidade("Rainforest Alliance / ATITUDES", "Fora de unidade de conservação",
                "Não produzir dentro de unidade de conservação sem autorização do órgão gestor.",
                dentroPi.Count > 0 ? StatusCriterio.IndicioNaoConformidade : dentroUs.Count > 0 ? StatusCriterio.Verificar : StatusCriterio.Conforme,
                dentroPi.Count > 0 ? "Imóvel dentro de UC de Proteção Integral: " + string.Join("; ", dentroPi.Select(u => u.Nome)) + ". Só é aceito com autorização/regularização junto ao órgão gestor."
                : dentroUs.Count > 0 ? "Imóvel dentro de UC de Uso Sustentável: " + string.Join("; ", dentroUs.Select(u => u.Nome)) + ". A produção pode ser permitida — confira o plano de manejo e guarde a anuência do órgão gestor, se exigida."
                : "O imóvel não está dentro de nenhuma unidade de conservação da camada consultada."));
            criterios.Add(new CriterioConformidade("Rainforest Alliance / ATITUDES", "Fora de zona de amortecimento",
                "Não produzir em zona de amortecimento de UC sem autorização.",
                za.Count > 0 ? StatusCriterio.Verificar : StatusCriterio.Conforme,
                za.Count > 0
                    ? string.Join(" ", za.Select(u => u.Situacao))
                    : $"Nenhuma zona de amortecimento informada nem UC (que exija zona) a menos de {_ucs.RaioAmortecimentoM / 1000.0:0.#} km.",
                $"Quando a UC não tem zona de amortecimento definida, foi usada a faixa de {_ucs.RaioAmortecimentoM:N0} m (Resolução CONAMA 428/2010). APA e RPPN não têm zona de amortecimento."));
        }

        criterios.Add(new CriterioConformidade("MapBiomas Alerta", "Alertas de desmatamento validados (desde 2019)",
            "Nenhum alerta de desmatamento dentro do imóvel.",
            ficha.TotalAlertas > 0 ? StatusCriterio.Verificar : StatusCriterio.Conforme,
            ficha.TotalAlertas > 0
                ? $"{ficha.TotalAlertas} alerta(s), {Fmt(ficha.AreaTotalAlertasHa)} ha no total — último em {ficha.UltimoAlerta:dd/MM/yyyy}."
                : "Nenhum alerta registrado para o imóvel.",
            "Atualize em \"Ver alertas\" na tela do imóvel antes de emitir o relatório."));
        fontes.Add("MapBiomas Alerta (alertas validados, com imagens de alta resolução antes/depois)");
        if (_copernicus.Configurado) fontes.Add("Sentinel-2 (Copernicus Data Space Ecosystem): imagens em cor real e NDVI, 10 m");
        else avisos.Add("Imagens de satélite Sentinel-2 desligadas: configure o Copernicus (AgroCert:Ambiental:Copernicus) para incluir as fotos de satélite antes × depois.");

        dto = dto with { Ucs = ucs, UcsGeoJson = ucsGeo };
        if (analise is null) return Resultado(dto);
        return Resultado(dto with
        {
            AnosComparados = analise.Anos,
            AnoAtual = analise.Ultimo,
            PorArea = analise.PorArea,
            Classes = analise.Classes,
            Imovel = analise.JanelaImovel,
            MapaMudanca = analise.MapaMudanca,
            MapaMudancaLimites = analise.MapaLimites,
            Areas = analise.Areas,
            ConversaoTotalHa = analise.Areas.Sum(a => a.AreaHa),
            ManchasPequenas = analise.ManchasPequenas,
            ManchasPequenasHa = analise.ManchasPequenasHa,
            RegeneracaoHa = analise.RegeneracaoHa,
            LegendaMudanca = analise.Legenda,
        });
    }

    private static CriterioConformidade SemDados(string marco, string motivo) => marco switch
    {
        "2014" => new("Rainforest Alliance", "Sem conversão de ecossistemas naturais desde 01/01/2014", "Nenhuma área de vegetação nativa convertida após 01/01/2014.", StatusCriterio.SemDados, "Não avaliado: " + motivo + "."),
        "2008" => new("ATITUDES / Código Florestal", "Supressão após 22/07/2008 só com autorização", "Supressão depois de 22/07/2008 precisa de autorização.", StatusCriterio.SemDados, "Não avaliado: " + motivo + "."),
        _ => new("ATITUDES", "Nenhuma supressão após 31/12/2020", "Depois de 31/12/2020 não é aceita supressão, mesmo autorizada.", StatusCriterio.SemDados, "Não avaliado: " + motivo + "."),
    };

    private static List<GeoUtil.Poligono> Ler(string? geo)
    {
        if (string.IsNullOrWhiteSpace(geo)) return new();
        try { return GeoUtil.LerGeoJson(geo); } catch { return new(); }
    }

    private static string Fmt(decimal v) => v.ToString(v < 10 ? "N1" : "N0", CultureInfo.GetCultureInfo("pt-BR"));
}
