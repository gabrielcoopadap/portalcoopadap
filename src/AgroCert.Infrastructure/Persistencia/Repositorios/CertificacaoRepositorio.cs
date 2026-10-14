using AgroCert.Application.Interfaces;
using AgroCert.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroCert.Infrastructure.Persistencia.Repositorios;

public class CertificacaoRepositorio : ICertificacaoRepositorio
{
    private readonly AgroCertDbContext _db;
    public CertificacaoRepositorio(AgroCertDbContext db) => _db = db;

    public Task<List<Certificacao>> ListarAsync(CancellationToken ct = default) =>
        _db.Certificacoes.OrderBy(c => c.Id).ToListAsync(ct);

    public Task<Certificacao?> ObterAsync(long id, CancellationToken ct = default) =>
        _db.Certificacoes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AdicionarAsync(Certificacao certificacao, CancellationToken ct = default) =>
        await _db.Certificacoes.AddAsync(certificacao, ct);

    public async Task ExcluirAsync(long id, CancellationToken ct = default)
    {
        var c = await ObterAsync(id, ct);
        if (c != null) _db.Certificacoes.Remove(c);
    }

    public Task<int> ContarAsync(CancellationToken ct = default) => _db.Certificacoes.CountAsync(ct);

    public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
