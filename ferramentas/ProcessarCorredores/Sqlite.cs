using System.Buffers.Binary;
using System.Text;

namespace ProcessarCorredores;

public sealed class SqliteLeitor : IDisposable
{
    private readonly FileStream _arq;
    private readonly int _pagina;
    private readonly int _util;
    private readonly Encoding _texto;

    public SqliteLeitor(string caminho)
    {
        _arq = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16);
        var cab = new byte[100];
        _arq.ReadExactly(cab);
        if (Encoding.ASCII.GetString(cab, 0, 15) != "SQLite format 3")
            throw new InvalidDataException("Arquivo não é um banco SQLite/GeoPackage.");
        var p = BinaryPrimitives.ReadUInt16BigEndian(cab.AsSpan(16));
        _pagina = p == 1 ? 65536 : p;
        _util = _pagina - cab[20];
        var enc = BinaryPrimitives.ReadUInt32BigEndian(cab.AsSpan(56));
        _texto = enc switch { 2 => Encoding.Unicode, 3 => Encoding.BigEndianUnicode, _ => Encoding.UTF8 };
    }

    public Dictionary<string, (long Raiz, List<string> Colunas)> Tabelas()
    {
        var r = new Dictionary<string, (long, List<string>)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, linha) in Linhas(1))
        {
            if (linha.Length < 5 || linha[0] as string != "table") continue;
            var nome = (string)linha[1]!;
            var raiz = Convert.ToInt64(linha[3]);
            r[nome] = (raiz, ColunasDoSql(linha[4] as string ?? ""));
        }
        return r;
    }

    public IEnumerable<(long RowId, object?[] Valores)> Linhas(long raiz)
    {
        var pilha = new Stack<long>();
        pilha.Push(raiz);
        while (pilha.Count > 0)
        {
            var num = pilha.Pop();
            var pag = LerPagina(num);
            var ini = num == 1 ? 100 : 0;
            var tipo = pag[ini];
            int n = BinaryPrimitives.ReadUInt16BigEndian(pag.AsSpan(ini + 3));
            if (tipo == 0x05)
            {
                var direita = BinaryPrimitives.ReadUInt32BigEndian(pag.AsSpan(ini + 8));
                var filhos = new List<long>(n + 1);
                for (var i = 0; i < n; i++)
                {
                    int off = BinaryPrimitives.ReadUInt16BigEndian(pag.AsSpan(ini + 12 + i * 2));
                    filhos.Add(BinaryPrimitives.ReadUInt32BigEndian(pag.AsSpan(off)));
                }
                filhos.Add(direita);
                for (var i = filhos.Count - 1; i >= 0; i--) pilha.Push(filhos[i]);
            }
            else if (tipo == 0x0D)
            {
                var itens = new List<(long, object?[])>(n);
                for (var i = 0; i < n; i++)
                {
                    int off = BinaryPrimitives.ReadUInt16BigEndian(pag.AsSpan(ini + 8 + i * 2));
                    var (tam, a) = Varint(pag, off); off += a;
                    var (rowid, b) = Varint(pag, off); off += b;
                    var carga = Carga(pag, off, tam);
                    itens.Add((rowid, Registro(carga)));
                }
                foreach (var it in itens) yield return it;
            }
            else throw new InvalidDataException($"Página {num} com tipo inesperado ({tipo}).");
        }
    }

    private byte[] Carga(byte[] pag, int off, long tam)
    {
        var u = _util;
        var x = u - 35;
        if (tam <= x)
        {
            var c = new byte[tam];
            Array.Copy(pag, off, c, 0, tam);
            return c;
        }
        var m = ((u - 12) * 32 / 255) - 23;
        var k = m + ((tam - m) % (u - 4));
        var local = (int)(k <= x ? k : m);
        var saida = new byte[tam];
        Array.Copy(pag, off, saida, 0, local);
        long prox = BinaryPrimitives.ReadUInt32BigEndian(pag.AsSpan(off + local));
        var pos = local;
        while (prox != 0 && pos < tam)
        {
            var ov = LerPagina(prox);
            prox = BinaryPrimitives.ReadUInt32BigEndian(ov.AsSpan(0));
            var q = (int)Math.Min(u - 4, tam - pos);
            Array.Copy(ov, 4, saida, pos, q);
            pos += q;
        }
        return saida;
    }

    private object?[] Registro(byte[] c)
    {
        var (tamCab, a) = Varint(c, 0);
        var tipos = new List<long>();
        var p = a;
        while (p < tamCab) { var (t, b) = Varint(c, p); tipos.Add(t); p += b; }
        var dados = (int)tamCab;
        var vals = new object?[tipos.Count];
        for (var i = 0; i < tipos.Count; i++)
        {
            var t = tipos[i];
            switch (t)
            {
                case 0: vals[i] = null; break;
                case 1: vals[i] = (long)(sbyte)c[dados]; dados += 1; break;
                case 2: vals[i] = (long)BinaryPrimitives.ReadInt16BigEndian(c.AsSpan(dados)); dados += 2; break;
                case 3: vals[i] = (long)((((sbyte)c[dados]) << 16) | (c[dados + 1] << 8) | c[dados + 2]); dados += 3; break;
                case 4: vals[i] = (long)BinaryPrimitives.ReadInt32BigEndian(c.AsSpan(dados)); dados += 4; break;
                case 5:
                {
                    long v = (sbyte)c[dados];
                    for (var k = 1; k < 6; k++) v = (v << 8) | c[dados + k];
                    vals[i] = v; dados += 6; break;
                }
                case 6: vals[i] = BinaryPrimitives.ReadInt64BigEndian(c.AsSpan(dados)); dados += 8; break;
                case 7: vals[i] = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(c.AsSpan(dados))); dados += 8; break;
                case 8: vals[i] = 0L; break;
                case 9: vals[i] = 1L; break;
                default:
                    if (t >= 12 && t % 2 == 0) { var n = (int)((t - 12) / 2); vals[i] = c.AsSpan(dados, n).ToArray(); dados += n; }
                    else if (t >= 13) { var n = (int)((t - 13) / 2); vals[i] = _texto.GetString(c, dados, n); dados += n; }
                    break;
            }
        }
        return vals;
    }

    private byte[] LerPagina(long num)
    {
        var b = new byte[_pagina];
        _arq.Seek((num - 1) * _pagina, SeekOrigin.Begin);
        _arq.ReadExactly(b);
        return b;
    }

    private static (long Valor, int Bytes) Varint(byte[] b, int off)
    {
        long v = 0;
        for (var i = 0; i < 8; i++)
        {
            var x = b[off + i];
            v = (v << 7) | (long)(x & 0x7F);
            if ((x & 0x80) == 0) return (v, i + 1);
        }
        v = (v << 8) | b[off + 8];
        return (v, 9);
    }

    private static List<string> ColunasDoSql(string sql)
    {
        var ini = sql.IndexOf('(');
        var fim = sql.LastIndexOf(')');
        var cols = new List<string>();
        if (ini < 0 || fim <= ini) return cols;
        var corpo = sql[(ini + 1)..fim];
        var nivel = 0;
        var atual = new StringBuilder();
        void Fechar()
        {
            var parte = atual.ToString().Trim();
            atual.Clear();
            if (parte.Length == 0) return;
            var up = parte.ToUpperInvariant();
            if (up.StartsWith("CONSTRAINT") || up.StartsWith("PRIMARY KEY") || up.StartsWith("UNIQUE") || up.StartsWith("CHECK") || up.StartsWith("FOREIGN KEY")) return;
            var nome = parte.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim('"', '`', '[', ']');
            cols.Add(nome);
        }
        foreach (var ch in corpo)
        {
            if (ch == '(') nivel++;
            else if (ch == ')') nivel--;
            if (ch == ',' && nivel == 0) { Fechar(); continue; }
            atual.Append(ch);
        }
        Fechar();
        return cols;
    }

    public void Dispose() => _arq.Dispose();
}
