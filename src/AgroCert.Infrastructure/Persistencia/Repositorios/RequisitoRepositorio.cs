using AgroCert.Application.Interfaces;
using AgroCert.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroCert.Infrastructure.Persistencia.Repositorios;

public class RequisitoRepositorio : IRequisitoRepositorio
{
    private readonly AgroCertDbContext _db;
    public RequisitoRepositorio(AgroCertDbContext db) => _db = db;

    public Task<List<Requisito>> ListarAsync(CancellationToken ct = default) =>
        _db.Requisitos.OrderBy(r => r.Id).ToListAsync(ct);

    public Task<Requisito?> ObterAsync(long id, CancellationToken ct = default) =>
        _db.Requisitos.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AdicionarAsync(Requisito requisito, CancellationToken ct = default) =>
        await _db.Requisitos.AddAsync(requisito, ct);

    public Task AdicionarVariosAsync(IEnumerable<Requisito> requisitos, CancellationToken ct = default) =>
        _db.Requisitos.AddRangeAsync(requisitos, ct);

    public async Task ExcluirAsync(long id, CancellationToken ct = default)
    {
        var r = await ObterAsync(id, ct);
        if (r == null) return;
        // desfaz o vínculo com apontamentos já carregados/rastreados (no banco a FK é SET NULL)
        await _db.Apontamentos.Where(a => a.RequisitoId == id).LoadAsync(ct);
        _db.Requisitos.Remove(r);
    }

    public Task<int> ContarAsync(CancellationToken ct = default) => _db.Requisitos.CountAsync(ct);

    public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
