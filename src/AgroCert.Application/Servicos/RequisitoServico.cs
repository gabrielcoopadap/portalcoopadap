using AgroCert.Application.Interfaces;
using AgroCert.Domain.Entidades;
using AgroCert.Domain.Excecoes;

namespace AgroCert.Application.Servicos;

public class RequisitoServico
{
    private readonly IRequisitoRepositorio _repo;
    private readonly IExportadorListaMestra _exportador;

    public RequisitoServico(IRequisitoRepositorio repo, IExportadorListaMestra exportador)
    {
        _repo = repo;
        _exportador = exportador;
    }

    public Task<List<Requisito>> ListarAsync(CancellationToken ct = default) => _repo.ListarAsync(ct);

    public async Task<Requisito> ObterAsync(long id, CancellationToken ct = default) =>
        await _repo.ObterAsync(id, ct) ?? throw new NaoEncontradoException("Requisito não encontrado: " + id);

    public async Task<Requisito> CriarAsync(Requisito dados, CancellationToken ct = default)
    {
        Validar(dados);
        dados.Id = 0;
        dados.Titulo = dados.Titulo!.Trim();
        dados.Referencias = LimparReferencias(dados.Referencias);
        await _repo.AdicionarAsync(dados, ct);
        await _repo.SalvarAsync(ct);
        return dados;
    }

    public async Task<Requisito> AtualizarAsync(long id, Requisito dados, CancellationToken ct = default)
    {
        Validar(dados);
        var r = await ObterAsync(id, ct);
        r.Codigo = dados.Codigo;
        r.Titulo = dados.Titulo!.Trim();
        r.Descricao = dados.Descricao;
        r.Tema = dados.Tema;
        r.Prioridade = dados.Prioridade;
        r.Status = dados.Status;
        r.Responsavel = dados.Responsavel;
        r.Periodicidade = dados.Periodicidade;
        r.Prazo = dados.Prazo;
        r.Observacao = dados.Observacao;
        r.Referencias.Clear();
        r.Referencias.AddRange(LimparReferencias(dados.Referencias));
        await _repo.SalvarAsync(ct);
        return r;
    }

    public async Task ExcluirAsync(long id, CancellationToken ct = default)
    {
        await _repo.ExcluirAsync(id, ct);
        await _repo.SalvarAsync(ct);
    }

    public static List<string> NormasPresentes(IEnumerable<Requisito> requisitos) =>
        requisitos.SelectMany(r => r.Referencias).Select(x => x.Norma).OfType<string>()
            .Where(n => n.Length > 0).Distinct().OrderBy(n => n, StringComparer.CurrentCulture).ToList();

    public static List<string> TemasPresentes(IEnumerable<Requisito> requisitos) =>
        requisitos.Select(r => r.Tema).OfType<string>().Where(t => t.Length > 0)
            .Distinct().OrderBy(t => t, StringComparer.CurrentCulture).ToList();

    public static IEnumerable<Requisito> Filtrar(IEnumerable<Requisito> requisitos, string? busca, string? tema, string? status, string? norma, string? prioridade = null)
    {
        foreach (var r in requisitos)
        {
            if (!string.IsNullOrWhiteSpace(busca))
            {
                var alvo = $"{r.Titulo} {r.Descricao} {r.Tema} {string.Join(" ", r.Referencias.Select(x => x.Codigo))}";
                if (!alvo.Contains(busca.Trim(), StringComparison.CurrentCultureIgnoreCase)) continue;
            }
            if (!string.IsNullOrEmpty(tema) && r.Tema != tema) continue;
            if (!string.IsNullOrEmpty(status) && r.Status != status) continue;
            if (!string.IsNullOrEmpty(norma) && !r.Referencias.Any(x => x.Norma == norma)) continue;
            if (!string.IsNullOrEmpty(prioridade) && r.Prioridade != prioridade) continue;
            yield return r;
        }
    }

    public async Task<(byte[] Conteudo, string NomeArquivo)> ExportarListaMestraAsync(string? certificacao, CancellationToken ct = default)
    {
        var todos = await _repo.ListarAsync(ct);
        var lista = string.IsNullOrEmpty(certificacao)
            ? todos
            : todos.Where(r => r.Referencias.Any(x => x.Norma == certificacao)).ToList();
        var bytes = _exportador.Gerar(lista, certificacao);
        var sufixo = string.IsNullOrEmpty(certificacao) ? "todas" : System.Text.RegularExpressions.Regex.Replace(certificacao, @"[^\w]+", "-").ToLowerInvariant();
        return (bytes, $"lista-mestra-{sufixo}.xlsx");
    }

    private static void Validar(Requisito r)
    {
        if (string.IsNullOrWhiteSpace(r.Titulo)) throw new RegraNegocioException("Informe o título do requisito.");
    }

    private static List<Referencia> LimparReferencias(IEnumerable<Referencia>? refs) =>
        (refs ?? Enumerable.Empty<Referencia>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo))
            .Select(x => new Referencia { Norma = x.Norma, Versao = x.Versao?.Trim(), Codigo = x.Codigo!.Trim() })
            .ToList();
}
