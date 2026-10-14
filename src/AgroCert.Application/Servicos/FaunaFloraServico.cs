using AgroCert.Application.Dtos;
using AgroCert.Application.Interfaces;
using AgroCert.Application.Util;
using AgroCert.Domain.Entidades;
using AgroCert.Domain.Enums;
using AgroCert.Domain.Excecoes;

namespace AgroCert.Application.Servicos;

public class FaunaFloraServico
{
    private readonly IFaunaFloraRepositorio _repo;
    private readonly IAmbientalRepositorio _ambiental;
    private readonly IArmazenamentoArquivos _arquivos;

    public FaunaFloraServico(IFaunaFloraRepositorio repo, IAmbientalRepositorio ambiental, IArmazenamentoArquivos arquivos)
    {
        _repo = repo;
        _ambiental = ambiental;
        _arquivos = arquivos;
    }

    public async Task<PainelFaunaFloraDto> MontarPainelAsync(int meses = 6, CancellationToken ct = default)
    {
        var agora = DateTime.Now;
        var especies = await _repo.ListarEspeciesAsync(ct);
        var registros = await _repo.ListarRegistrosAsync(ct);
        var propriedades = await _ambiental.ListarPropriedadesAsync(ct);

        int EspeciesCom(Reino r, DateTime de, DateTime ate) =>
            registros.Where(x => x.Especie.Reino == r && x.DataHora >= de && x.DataHora < ate).Select(x => x.EspecieId).Distinct().Count();

        KpiBio KpiEspecies(Reino r, string rotulo, string icone)
        {
            var total = registros.Where(x => x.Especie.Reino == r).Select(x => x.EspecieId).Distinct().Count();
            var ano = EspeciesCom(r, agora.AddYears(-1), agora.AddMinutes(1));
            var anoAnterior = EspeciesCom(r, agora.AddYears(-2), agora.AddYears(-1));
            return new KpiBio(rotulo, total.ToString(), icone, Variacao(ano, anoAnterior), "vs. último ano");
        }

        var ultimos30 = registros.Count(x => x.DataHora >= agora.AddDays(-30));
        var anteriores30 = registros.Count(x => x.DataHora >= agora.AddDays(-60) && x.DataHora < agora.AddDays(-30));
        var areaPreservacao = propriedades.Sum(p => (p.AreaReservaLegalHa ?? 0) + (p.AreaAppHa ?? 0));

        var indicadores = new List<KpiBio>
        {
            KpiEspecies(Reino.FAUNA, "Espécies de fauna registradas", "pets"),
            KpiEspecies(Reino.FLORA, "Espécies de flora identificadas", "eco"),
            new("Áreas de preservação", Rotulos.Numero(areaPreservacao, 1) + " ha", "park", null, "",
                propriedades.Count == 0 ? "Cadastre as propriedades no Ambiental" : $"Reserva Legal + APP · {propriedades.Count} propriedade(s)"),
            new("Registros recentes (30 dias)", ultimos30.ToString(), "photo_camera", Variacao(ultimos30, anteriores30), "vs. mês anterior"),
        };

        var destaques = registros.OrderByDescending(r => r.DataHora)
            .GroupBy(r => r.EspecieId).Select(g => g.First())
            .Take(3).ToList();

        var cameras = registros.Where(r => r.Origem == OrigemRegistro.CAMERA)
            .OrderByDescending(r => r.DataHora).Take(8).ToList();

        List<EspecieDestaque> EmDestaque(Reino reino)
        {
            var inicio = agora.AddDays(-90);
            var inicioAnterior = agora.AddDays(-180);
            return registros.Where(r => r.Especie.Reino == reino && r.DataHora >= inicio)
                .GroupBy(r => r.Especie)
                .Select(g =>
                {
                    var anterior = registros.Count(r => r.EspecieId == g.Key.Id && r.DataHora >= inicioAnterior && r.DataHora < inicio);
                    var foto = g.Key.FotoUrl ?? g.OrderByDescending(r => r.DataHora).Select(r => r.FotoUrl).FirstOrDefault(f => f != null);
                    return new EspecieDestaque(g.Key, g.Count(), Variacao(g.Count(), anterior), foto);
                })
                .OrderByDescending(d => d.Registros).ThenBy(d => d.Especie.NomePopular)
                .Take(4).ToList();
        }

        meses = meses is 12 ? 12 : 6;
        var primeiroMes = new DateOnly(agora.Year, agora.Month, 1).AddMonths(-(meses - 1));
        var evolucao = Enumerable.Range(0, meses).Select(i =>
        {
            var mes = primeiroMes.AddMonths(i);
            bool NoMes(RegistroBiodiversidade r) => r.DataHora.Year == mes.Year && r.DataHora.Month == mes.Month;
            return new PontoMes(mes,
                registros.Count(r => NoMes(r) && r.Especie.Reino == Reino.FAUNA),
                registros.Count(r => NoMes(r) && r.Especie.Reino == Reino.FLORA));
        }).ToList();

        var especiesFauna = registros.Where(r => r.Especie.Reino == Reino.FAUNA).Select(r => r.Especie).DistinctBy(e => e.Id).ToList();
        var totalFauna = especiesFauna.Count;
        var distribuicao = Biodiversidade.GruposFauna
            .Select(g => (g, n: especiesFauna.Count(e => Biodiversidade.Grupo(Reino.FAUNA, e.Grupo).Nome == g.Nome)))
            .Where(x => x.n > 0)
            .Select(x => new FatiaGrupo(x.g.Plural, x.g.Cor, x.n, totalFauna == 0 ? 0 : Math.Round(x.n * 100m / totalFauna, 0)))
            .ToList();

        return new PainelFaunaFloraDto(indicadores, destaques, cameras, EmDestaque(Reino.FAUNA), EmDestaque(Reino.FLORA),
            evolucao, distribuicao, totalFauna,
            registros.Where(r => r.Latitude.HasValue && r.Longitude.HasValue).ToList(),
            propriedades, especies.OrderBy(e => e.NomePopular).ToList());
    }

    private static decimal? Variacao(int atual, int anterior) =>
        anterior == 0 ? null : Math.Round((atual - anterior) * 100m / anterior, 0);

    public async Task<List<Especie>> ListarEspeciesAsync(CancellationToken ct = default) =>
        (await _repo.ListarEspeciesAsync(ct)).OrderBy(e => e.NomePopular).ToList();

    public async Task<Especie> ObterEspecieAsync(long id, CancellationToken ct = default) =>
        await _repo.ObterEspecieAsync(id, ct) ?? throw new NaoEncontradoException("Espécie não encontrada: " + id);

    public async Task<Especie> CriarEspecieAsync(EspecieRequest req, ArquivoEnviado? foto, CancellationToken ct = default)
    {
        var e = new Especie();
        Preencher(e, req);
        e.FotoUrl = await _arquivos.SalvarAsync(foto, "fauna-flora", ct);
        await _repo.AdicionarEspecieAsync(e, ct);
        await _repo.SalvarAsync(ct);
        return e;
    }

    public async Task<Especie> AtualizarEspecieAsync(long id, EspecieRequest req, ArquivoEnviado? foto, CancellationToken ct = default)
    {
        var e = await ObterEspecieAsync(id, ct);
        Preencher(e, req);
        var nova = await _arquivos.SalvarAsync(foto, "fauna-flora", ct);
        if (nova != null) e.FotoUrl = nova;
        await _repo.SalvarAsync(ct);
        return e;
    }

    public async Task ExcluirEspecieAsync(long id, CancellationToken ct = default)
    {
        var total = (await _repo.ListarRegistrosAsync(ct)).Count(r => r.EspecieId == id);
        if (total > 0)
            throw new RegraNegocioException($"Esta espécie tem {total} registro(s) de ocorrência. Exclua os registros antes de excluir a espécie.");
        await _repo.ExcluirEspecieAsync(id, ct);
        await _repo.SalvarAsync(ct);
    }

    private static void Preencher(Especie e, EspecieRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NomePopular)) throw new RegraNegocioException("Informe o nome popular da espécie.");
        if (!Enum.TryParse<Reino>(req.Reino, true, out var reino) || !Enum.IsDefined(reino))
            throw new RegraNegocioException("Informe se a espécie é de fauna ou flora.");
        e.NomePopular = req.NomePopular.Trim();
        e.NomeCientifico = string.IsNullOrWhiteSpace(req.NomeCientifico) ? null : req.NomeCientifico.Trim();
        e.Reino = reino;
        e.Grupo = Biodiversidade.Grupo(reino, req.Grupo).Nome;
        e.StatusConservacao = Biodiversidade.StatusConservacao.Any(s => s.Codigo == req.StatusConservacao) ? req.StatusConservacao : "NA";
        e.Descricao = req.Descricao;
    }

    public async Task<List<RegistroBiodiversidade>> ListarRegistrosAsync(CancellationToken ct = default) =>
        await _repo.ListarRegistrosAsync(ct);

    public async Task<RegistroBiodiversidade> CriarRegistroAsync(NovoRegistroRequest req, ArquivoEnviado? foto, CancellationToken ct = default)
    {
        if (req.EspecieId is not long especieId) throw new RegraNegocioException("Selecione a espécie registrada.");
        var especie = await ObterEspecieAsync(especieId, ct);
        if (!Enum.TryParse<OrigemRegistro>(req.Origem, true, out var origem) || !Enum.IsDefined(origem))
            throw new RegraNegocioException("Informe como a ocorrência foi registrada (avistamento, câmera, vestígio ou coleta).");
        if (req.Latitude is < -90 or > 90 || req.Longitude is < -180 or > 180)
            throw new RegraNegocioException("Latitude/longitude inválidas.");
        if (req.DataHora > DateTime.Now.AddMinutes(5))
            throw new RegraNegocioException("A data do registro não pode estar no futuro.");

        Propriedade? propriedade = null;
        if (req.PropriedadeId is long pid) propriedade = await _ambiental.ObterPropriedadeAsync(pid, ct);

        var r = new RegistroBiodiversidade
        {
            Especie = especie,
            EspecieId = especie.Id,
            Propriedade = propriedade,
            PropriedadeId = propriedade?.Id,
            DataHora = req.DataHora ?? DateTime.Now,
            Origem = origem,
            Camera = origem == OrigemRegistro.CAMERA ? req.Camera?.Trim() : null,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            Area = req.Area,
            Quantidade = Math.Max(1, req.Quantidade ?? 1),
            Observacao = req.Observacao,
            RegistradoPor = req.RegistradoPor,
            FotoUrl = await _arquivos.SalvarAsync(foto, "fauna-flora", ct),
        };
        await _repo.AdicionarRegistroAsync(r, ct);
        await _repo.SalvarAsync(ct);
        return r;
    }

    public async Task ExcluirRegistroAsync(long id, CancellationToken ct = default)
    {
        await _repo.ExcluirRegistroAsync(id, ct);
        await _repo.SalvarAsync(ct);
    }
}
