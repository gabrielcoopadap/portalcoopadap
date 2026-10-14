using System.Buffers.Binary;

namespace ProcessarCorredores;

public sealed class Poligono
{
    public List<double[]> Aneis { get; } = new();
}

public sealed class Feicao
{
    public Dictionary<string, object?> Atributos { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Poligono> Poligonos { get; } = new();
    public string Texto(string campo) => Atributos.TryGetValue(campo, out var v) ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "" : "";
}

public static class Gpkg
{
    public static IEnumerable<Feicao> Ler(string caminho)
    {
        using var db = new SqliteLeitor(caminho);
        var tabelas = db.Tabelas();
        string? tabela = null, colunaGeo = null;
        if (tabelas.TryGetValue("gpkg_geometry_columns", out var gc))
        {
            foreach (var (_, v) in db.Linhas(gc.Raiz))
            {
                var nomes = gc.Colunas;
                tabela = v[nomes.IndexOf("table_name")] as string;
                colunaGeo = v[nomes.IndexOf("column_name")] as string;
                break;
            }
        }
        if (tabela is null || colunaGeo is null || !tabelas.TryGetValue(tabela, out var t))
            throw new InvalidDataException("GeoPackage sem tabela de geometria.");

        var cols = t.Colunas;
        var iGeo = cols.FindIndex(c => c.Equals(colunaGeo, StringComparison.OrdinalIgnoreCase));
        foreach (var (rowid, v) in db.Linhas(t.Raiz))
        {
            var f = new Feicao();
            for (var i = 0; i < cols.Count && i < v.Length; i++)
                if (i != iGeo) f.Atributos[cols[i]] = v[i] ?? (i == 0 ? rowid : null);
            if (iGeo >= 0 && iGeo < v.Length && v[iGeo] is byte[] blob) LerGeometria(blob, f.Poligonos);
            yield return f;
        }
    }

    private static void LerGeometria(byte[] b, List<Poligono> saida)
    {
        if (b.Length < 8 || b[0] != (byte)'G' || b[1] != (byte)'P') { Wkb(b, 0, saida); return; }
        var flags = b[3];
        var env = (flags >> 1) & 7;
        var tamEnv = env switch { 1 => 32, 2 or 3 => 48, 4 => 64, _ => 0 };
        if ((flags & 0x10) != 0) return;
        Wkb(b, 8 + tamEnv, saida);
    }

    private static int Wkb(byte[] b, int p, List<Poligono> saida)
    {
        var le = b[p] == 1;
        uint U32(int o) => le ? BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o)) : BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o));
        double D(int o) => BitConverter.Int64BitsToDouble(le ? BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(o)) : BinaryPrimitives.ReadInt64BigEndian(b.AsSpan(o)));
        var tipoBruto = U32(p + 1);
        p += 5;
        // dimensões: ISO (1000/2000/3000) ou EWKB (flags nos bits altos)
        var temZ = (tipoBruto & 0x80000000) != 0 || (tipoBruto % 10000) / 1000 is 1 or 3;
        var temM = (tipoBruto & 0x40000000) != 0 || (tipoBruto % 10000) / 1000 is 2 or 3;
        var tipo = (tipoBruto & 0x0FFFFFFF) % 1000;
        var dims = 2 + (temZ ? 1 : 0) + (temM ? 1 : 0);
        switch (tipo)
        {
            case 3:
            {
                var pol = new Poligono();
                var nAneis = U32(p); p += 4;
                for (var r = 0; r < nAneis; r++)
                {
                    var n = (int)U32(p); p += 4;
                    var anel = new double[n * 2];
                    for (var i = 0; i < n; i++) { anel[i * 2] = D(p); anel[i * 2 + 1] = D(p + 8); p += 8 * dims; }
                    pol.Aneis.Add(anel);
                }
                saida.Add(pol);
                return p;
            }
            case 6:
            case 7:
            {
                var n = U32(p); p += 4;
                for (var i = 0; i < n; i++) p = Wkb(b, p, saida);
                return p;
            }
            default:
                return b.Length;
        }
    }
}
