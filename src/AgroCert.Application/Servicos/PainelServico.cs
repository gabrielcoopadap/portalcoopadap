using AgroCert.Application.Dtos;
using AgroCert.Domain.Entidades;

namespace AgroCert.Application.Servicos;

public class PainelServico
{
    private static readonly Dictionary<string, int> OrdemPrioridade = new() { ["Crítica"] = 0, ["Maior"] = 1, ["Menor"] = 2 };

    private readonly CertificacaoServico _certificacoes;
    private readonly RequisitoServico _requisitos;
    private readonly VistoriaServico _vistorias;

    public PainelServico(CertificacaoServico certificacoes, RequisitoServico requisitos, VistoriaServico vistorias)
    {
        _certificacoes = certificacoes;
        _requisitos = requisitos;
        _vistorias = vistorias;
    }

    public async Task<PainelDto> MontarAsync(CancellationToken ct = default)
    {
        var certs = await _certificacoes.ListarAsync(ct);
        var reqs = await _requisitos.ListarAsync(ct);
        var pendencias = await _vistorias.ListarPendenciasAsync(ct);

        var naoConformes = reqs.Where(r => r.Status == "Não Conforme").ToList();
        var abertos = reqs.Count(r => r.Status is "Pendente" or "Em Andamento");
        var vencidas = pendencias.Count(p => p.DiasRestantes < 0);

        var indicadores = new List<IndicadorPainel>
        {
            new("Certificações Ativas", certs.Count, $"{certs.Count(c => c.Status == "Conforme")} conformes", "#1f8a4c", "sparkle"),
            new("Não Conformidades", naoConformes.Count, $"{naoConformes.Count(r => r.Prioridade == "Crítica")} críticas", "#c0392b", "alerta"),
            new("Pendências", abertos, "requisitos em aberto", "#c98a1a", "relogio"),
            new("Pendências de Vistoria", pendencias.Count, $"{vencidas} com prazo vencido", "#1f6fb2", "vistoria"),
        };

        var acoes = reqs
            .Where(r => !string.IsNullOrEmpty(r.Status) && r.Status != "Conforme")
            .OrderBy(r => OrdemPrioridade.GetValueOrDefault(r.Prioridade ?? "", 3))
            .ThenBy(r => r.Prazo.HasValue ? 0 : 1)
            .ThenBy(r => r.Prazo)
            .Take(5)
            .ToList();

        return new PainelDto(indicadores, certs, acoes, pendencias.Take(5).ToList());
    }
}
