using AgroCert.Application.Interfaces;
using AgroCert.Domain.Entidades;
using AgroCert.Domain.Excecoes;

namespace AgroCert.Application.Servicos;

public class CertificacaoServico
{
    private readonly ICertificacaoRepositorio _repo;
    private readonly IRequisitoRepositorio _requisitos;

    public CertificacaoServico(ICertificacaoRepositorio repo, IRequisitoRepositorio requisitos)
    {
        _repo = repo;
        _requisitos = requisitos;
    }

    public async Task<List<Certificacao>> ListarAsync(CancellationToken ct = default)
    {
        var certs = await _repo.ListarAsync(ct);
        var reqs = await _requisitos.ListarAsync(ct);
        return Conformidade.Aplicar(certs, reqs);
    }

    public async Task<Certificacao> ObterAsync(long id, CancellationToken ct = default)
    {
        var c = await _repo.ObterAsync(id, ct) ?? throw new NaoEncontradoException("Certificação não encontrada: " + id);
        var reqs = Conformidade.RequisitosDaCertificacao(await _requisitos.ListarAsync(ct), c.Nome);
        return Conformidade.Aplicar(new[] { c }, reqs)[0];
    }

    public async Task<Certificacao> CriarAsync(Certificacao dados, CancellationToken ct = default)
    {
        Validar(dados);
        dados.Id = 0;
        Normalizar(dados);
        await _repo.AdicionarAsync(dados, ct);
        await _repo.SalvarAsync(ct);
        return dados;
    }

    public async Task<Certificacao> AtualizarAsync(long id, Certificacao dados, CancellationToken ct = default)
    {
        Validar(dados);
        var c = await _repo.ObterAsync(id, ct) ?? throw new NaoEncontradoException("Certificação não encontrada: " + id);
        Normalizar(dados);
        c.Sigla = dados.Sigla; c.Nome = dados.Nome; c.Escopo = dados.Escopo; c.Status = dados.Status; c.Cor = dados.Cor;
        c.Conformidade = dados.Conformidade; c.TotalRequisitos = dados.TotalRequisitos; c.Conformes = dados.Conformes;
        c.Pendentes = dados.Pendentes; c.NaoConformes = dados.NaoConformes; c.Organismo = dados.Organismo;
        c.ValidoAte = dados.ValidoAte; c.UltimaAuditoria = dados.UltimaAuditoria; c.ProximaAuditoria = dados.ProximaAuditoria;
        c.Categorias.Clear();
        c.Categorias.AddRange(dados.Categorias);
        await _repo.SalvarAsync(ct);
        return c;
    }

    public async Task ExcluirAsync(long id, CancellationToken ct = default)
    {
        await _repo.ExcluirAsync(id, ct);
        await _repo.SalvarAsync(ct);
    }

    private static void Validar(Certificacao c)
    {
        if (string.IsNullOrWhiteSpace(c.Nome)) throw new RegraNegocioException("Informe o nome da certificação.");
    }

    private static void Normalizar(Certificacao c)
    {
        c.Nome = c.Nome.Trim();
        var sigla = string.IsNullOrWhiteSpace(c.Sigla) ? c.Nome[..Math.Min(2, c.Nome.Length)] : c.Sigla.Trim();
        c.Sigla = sigla.ToUpperInvariant();
        c.Cor = string.IsNullOrWhiteSpace(c.Cor) ? "#5e9a42" : c.Cor;
        c.Categorias = c.Categorias.Where(x => !string.IsNullOrWhiteSpace(x.Nome)).ToList();
    }
}
