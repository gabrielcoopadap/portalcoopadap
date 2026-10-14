using AgroCert.Application.Interfaces;
using AgroCert.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroCert.Infrastructure.Persistencia.Repositorios;

public class FaunaFloraRepositorio : IFaunaFloraRepositorio
{
    private readonly AgroCertDbContext _db;
    public FaunaFloraRepositorio(AgroCertDbContext db) => _db = db;

    public Task<List<Especie>> ListarEspeciesAsync(CancellationToken ct = default) => _db.Especies.ToListAsync(ct);

    public Task<Especie?> ObterEspecieAsync(long id, CancellationToken ct = default) =>
        _db.Especies.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task AdicionarEspecieAsync(Especie especie, CancellationToken ct = default) =>
        await _db.Especies.AddAsync(especie, ct);

    public async Task ExcluirEspecieAsync(long id, CancellationToken ct = default)
    {
        var e = await ObterEspecieAsync(id, ct);
        if (e != null) _db.Especies.Remove(e);
    }

    public async Task<List<RegistroBiodiversidade>> ListarRegistrosAsync(CancellationToken ct = default) =>
        (await _db.RegistrosBiodiversidade.Include(r => r.Especie).Include(r => r.Propriedade).ToListAsync(ct))
            .OrderByDescending(r => r.DataHora).ToList();

    public Task<RegistroBiodiversidade?> ObterRegistroAsync(long id, CancellationToken ct = default) =>
        _db.RegistrosBiodiversidade.Include(r => r.Especie).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AdicionarRegistroAsync(RegistroBiodiversidade registro, CancellationToken ct = default) =>
        await _db.RegistrosBiodiversidade.AddAsync(registro, ct);

    public async Task ExcluirRegistroAsync(long id, CancellationToken ct = default)
    {
        var r = await _db.RegistrosBiodiversidade.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r != null) _db.RegistrosBiodiversidade.Remove(r);
    }

    public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
