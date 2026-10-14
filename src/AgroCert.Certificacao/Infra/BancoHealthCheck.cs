using AgroCert.Infrastructure.Persistencia;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgroCert.Certificacao.Infra;

public class BancoHealthCheck : IHealthCheck
{
    private readonly AgroCertDbContext _db;
    public BancoHealthCheck(AgroCertDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        await _db.Database.CanConnectAsync(ct)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("PostgreSQL indisponível.");
}
