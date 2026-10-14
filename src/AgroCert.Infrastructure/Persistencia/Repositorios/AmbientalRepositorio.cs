using AgroCert.Application.Interfaces;
using AgroCert.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroCert.Infrastructure.Persistencia.Repositorios;

public class AmbientalRepositorio : IAmbientalRepositorio
{
    private readonly AgroCertDbContext _db;
    public AmbientalRepositorio(AgroCertDbContext db) => _db = db;

    public Task<List<Propriedade>> ListarPropriedadesAsync(CancellationToken ct = default) =>
        _db.Propriedades.ToListAsync(ct);

    public Task<Propriedade?> ObterPropriedadeAsync(long id, CancellationToken ct = default) =>
        _db.Propriedades.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Propriedade?> ObterPropriedadePorCarAsync(string carCodigo, CancellationToken ct = default) =>
        _db.Propriedades.FirstOrDefaultAsync(p => p.CarCodigo == carCodigo, ct);

    public async Task AdicionarPropriedadeAsync(Propriedade propriedade, CancellationToken ct = default) =>
        await _db.Propriedades.AddAsync(propriedade, ct);

    public async Task ExcluirPropriedadeAsync(long id, CancellationToken ct = default)
    {
        var p = await _db.Propriedades
            .Include(x => x.Alertas)
            .Include(x => x.LeiturasNdvi)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p != null) _db.Propriedades.Remove(p);
    }

    public async Task<bool> ExisteAlertaAsync(long propriedadeId, string? codigoAlerta, string fonte, CancellationToken ct = default)
    {
        if (await _db.AlertasDesmatamento.AnyAsync(a => a.PropriedadeId == propriedadeId && a.CodigoAlerta == codigoAlerta && a.Fonte == fonte, ct))
            return true;
        // também considera os que foram adicionados nesta mesma sincronização e ainda não foram salvos
        return _db.ChangeTracker.Entries<AlertaDesmatamento>()
            .Any(e => e.State == EntityState.Added && e.Entity.PropriedadeId == propriedadeId
                      && e.Entity.CodigoAlerta == codigoAlerta && e.Entity.Fonte == fonte);
    }

    public async Task AdicionarAlertaAsync(AlertaDesmatamento alerta, CancellationToken ct = default) =>
        await _db.AlertasDesmatamento.AddAsync(alerta, ct);

    public async Task<List<AlertaDesmatamento>> ListarAlertasAsync(long propriedadeId, CancellationToken ct = default) =>
        (await _db.AlertasDesmatamento.Where(a => a.PropriedadeId == propriedadeId).ToListAsync(ct))
            .OrderByDescending(a => a.DataDeteccao).ToList();

    public Task<List<LeituraNdvi>> ListarNdviAsync(long propriedadeId, CancellationToken ct = default) =>
        _db.LeiturasNdvi.Where(l => l.PropriedadeId == propriedadeId).OrderBy(l => l.DataImagem).ToListAsync(ct);

    public Task<LeituraNdvi?> ObterNdviAsync(long propriedadeId, DateOnly dataImagem, CancellationToken ct = default) =>
        _db.LeiturasNdvi.FirstOrDefaultAsync(l => l.PropriedadeId == propriedadeId && l.DataImagem == dataImagem, ct);

    public async Task AdicionarNdviAsync(LeituraNdvi leitura, CancellationToken ct = default) =>
        await _db.LeiturasNdvi.AddAsync(leitura, ct);

    public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
