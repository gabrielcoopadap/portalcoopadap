using AgroCert.Domain.Entidades;

namespace AgroCert.Application.Servicos;

public static class Conformidade
{
    private static readonly Dictionary<string, string> NormaParaCertificacao = new()
    {
        ["Rainforest Alliance"] = "Rainforest Alliance",
        ["GlobalG.A.P."] = "GlobalG.A.P.",
        ["GRASP"] = "GlobalG.A.P.",
        ["C.A.F.E. Practices"] = "CAFE Practices",
        ["SMETA"] = "SMETA",
        ["ATITUDES"] = "ATITUDES",
    };

    public sealed record Estatistica(int Total, int Conformes, int Pendentes, int NaoConformes)
    {
        public int Percentual => Total == 0 ? 0 : (int)Math.Round(Conformes * 100.0 / Total, MidpointRounding.AwayFromZero);
    }

    public static IEnumerable<string> CertificacoesDoRequisito(Requisito r) =>
        r.Referencias
            .Where(x => x.Norma != null)
            .Select(x => NormaParaCertificacao.TryGetValue(x.Norma!, out var c) ? c : x.Norma!)
            .Distinct();

    public static Dictionary<string, Estatistica> PorCertificacao(IEnumerable<Requisito> requisitos)
    {
        var acc = new Dictionary<string, (int t, int c, int p, int n)>();
        foreach (var r in requisitos)
            foreach (var cert in CertificacoesDoRequisito(r))
            {
                var s = acc.GetValueOrDefault(cert);
                s.t++;
                if (r.Status == "Conforme") s.c++;
                else if (r.Status == "Não Conforme") s.n++;
                else s.p++;
                acc[cert] = s;
            }
        return acc.ToDictionary(kv => kv.Key, kv => new Estatistica(kv.Value.t, kv.Value.c, kv.Value.p, kv.Value.n));
    }

    public static List<Certificacao> Aplicar(IEnumerable<Certificacao> certificacoes, IEnumerable<Requisito> requisitos)
    {
        var stats = PorCertificacao(requisitos);
        return certificacoes.Select(c =>
        {
            var copia = Copiar(c);
            if (stats.TryGetValue(c.Nome, out var s))
            {
                copia.Conformidade = s.Percentual;
                copia.Conformes = s.Conformes;
                copia.Pendentes = s.Pendentes;
                copia.NaoConformes = s.NaoConformes;
                copia.TotalRequisitos = s.Total;
            }
            return copia;
        }).ToList();
    }

    public static IEnumerable<Requisito> RequisitosDaCertificacao(IEnumerable<Requisito> requisitos, string nomeCertificacao) =>
        requisitos.Where(r => CertificacoesDoRequisito(r).Contains(nomeCertificacao));

    private static Certificacao Copiar(Certificacao c) => new()
    {
        Id = c.Id, Sigla = c.Sigla, Nome = c.Nome, Escopo = c.Escopo, Status = c.Status, Cor = c.Cor,
        Conformidade = c.Conformidade, TotalRequisitos = c.TotalRequisitos, Conformes = c.Conformes,
        Pendentes = c.Pendentes, NaoConformes = c.NaoConformes, Organismo = c.Organismo,
        ValidoAte = c.ValidoAte, UltimaAuditoria = c.UltimaAuditoria, ProximaAuditoria = c.ProximaAuditoria,
        Categorias = c.Categorias.Select(x => new CategoriaConformidade { Nome = x.Nome, Conformes = x.Conformes, Total = x.Total }).ToList(),
    };
}
