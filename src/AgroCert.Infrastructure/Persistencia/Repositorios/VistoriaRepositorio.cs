using AgroCert.Application.Interfaces;
using AgroCert.Domain.Entidades;
using AgroCert.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AgroCert.Infrastructure.Persistencia.Repositorios;

public class VistoriaRepositorio : IVistoriaRepositorio
{
    private readonly AgroCertDbContext _db;
    public VistoriaRepositorio(AgroCertDbContext db) => _db = db;

    private IQueryable<Vistoria> ComApontamentos() =>
        _db.Vistorias
            .Include(v => v.Apontamentos)
            .ThenInclude(a => a.Requisito);

    public Task<List<Vistoria>> ListarAsync(CancellationToken ct = default) =>
        ComApontamentos().AsSplitQuery().ToListAsync(ct);

    public Task<Vistoria?> ObterAsync(long id, CancellationToken ct = default) =>
        ComApontamentos().AsSplitQuery().FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<List<string>> ListarCodigosDocumentoAsync(CancellationToken ct = default) =>
        _db.Vistorias.Select(v => v.CodigoDocumento).ToListAsync(ct);

    public async Task AdicionarAsync(Vistoria vistoria, CancellationToken ct = default) =>
        await _db.Vistorias.AddAsync(vistoria, ct);

    public async Task ExcluirAsync(long id, CancellationToken ct = default)
    {
        var v = await _db.Vistorias.Include(x => x.Apontamentos).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v != null) _db.Vistorias.Remove(v);
    }

    public Task<Apontamento?> ObterApontamentoAsync(long id, CancellationToken ct = default) =>
        _db.Apontamentos
            .Include(a => a.Vistoria)
            .Include(a => a.Requisito)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AdicionarApontamentoAsync(Apontamento apontamento, CancellationToken ct = default) =>
        await _db.Apontamentos.AddAsync(apontamento, ct);

    public async Task ExcluirApontamentoAsync(long id, CancellationToken ct = default)
    {
        var a = await _db.Apontamentos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a != null) _db.Apontamentos.Remove(a);
    }

    public Task<List<Apontamento>> ListarApontamentosPorStatusAsync(StatusApontamento status, CancellationToken ct = default) =>
        _db.Apontamentos
            .Include(a => a.Vistoria)
            .Include(a => a.Requisito)
            .Where(a => a.Status == status)
            .OrderBy(a => a.Prazo == null)
            .ThenBy(a => a.Prazo)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

    public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
