namespace ProcessarCorredores;

public static class Raster
{
    private const double Inf = 1e30;

    public static (float[] Dist2, int[] Sitio) Distancia(bool[] sitio, int w, int h, double px, double py)
    {
        var g = new double[w * h];
        var lin = new int[w * h];
        var wy2 = py * py;
        var wx2 = px * px;
        Parallel.For(0, w, () => new Buf(h), (x, _, b) =>
        {
            for (var y = 0; y < h; y++) b.F[y] = sitio[y * w + x] ? 0 : Inf;
            Dt1D(b, h, wy2);
            for (var y = 0; y < h; y++) { g[y * w + x] = b.D[y]; lin[y * w + x] = b.Arg[y]; }
            return b;
        }, _ => { });

        var d2 = new float[w * h];
        var sit = new int[w * h];
        Parallel.For(0, h, () => new Buf(w), (y, _, b) =>
        {
            var o = y * w;
            for (var x = 0; x < w; x++) b.F[x] = g[o + x];
            Dt1D(b, w, wx2);
            for (var x = 0; x < w; x++)
            {
                var c = b.Arg[x];
                if (c < 0 || b.D[x] >= Inf) { d2[o + x] = float.PositiveInfinity; sit[o + x] = -1; }
                else { d2[o + x] = (float)b.D[x]; sit[o + x] = lin[o + c] * w + c; }
            }
            return b;
        }, _ => { });
        return (d2, sit);
    }

    private sealed class Buf
    {
        public readonly double[] F, D, Z;
        public readonly int[] V, Arg;
        public Buf(int n) { F = new double[n]; D = new double[n]; Z = new double[n + 1]; V = new int[n]; Arg = new int[n]; }
    }

    private static void Dt1D(Buf b, int n, double w2)
    {
        var f = b.F; var v = b.V; var z = b.Z;
        var k = -1;
        for (var q = 0; q < n; q++)
        {
            if (f[q] >= Inf) continue;
            while (true)
            {
                if (k < 0) { k = 0; v[0] = q; z[0] = double.NegativeInfinity; break; }
                var p = v[k];
                var s = ((f[q] + w2 * q * q) - (f[p] + w2 * (double)p * p)) / (2 * w2 * (q - p));
                if (s <= z[k]) { k--; continue; }
                k++; v[k] = q; z[k] = s; break;
            }
        }
        if (k < 0)
        {
            for (var q = 0; q < n; q++) { b.D[q] = Inf; b.Arg[q] = -1; }
            return;
        }
        z[k + 1] = double.PositiveInfinity;
        var j = 0;
        for (var q = 0; q < n; q++)
        {
            while (z[j + 1] < q) j++;
            var dq = q - v[j];
            b.D[q] = w2 * dq * dq + f[v[j]];
            b.Arg[q] = v[j];
        }
    }

    public static (int[] Rotulos, int Quantidade) Rotular(bool[] m, int w, int h)
    {
        var rot = new int[w * h];
        var pai = new List<int> { 0 };
        int Raiz(int a) { while (pai[a] != a) { pai[a] = pai[pai[a]]; a = pai[a]; } return a; }
        void Unir(int a, int b) { a = Raiz(a); b = Raiz(b); if (a != b) { if (a < b) pai[b] = a; else pai[a] = b; } }

        for (var y = 0; y < h; y++)
        {
            var o = y * w;
            for (var x = 0; x < w; x++)
            {
                if (!m[o + x]) continue;
                var r = 0;
                void Ver(int i) { var l = rot[i]; if (l == 0) return; if (r == 0) r = l; else if (l != r) Unir(r, l); }
                if (x > 0) Ver(o + x - 1);
                if (y > 0)
                {
                    var a = o - w + x;
                    if (x > 0) Ver(a - 1);
                    Ver(a);
                    if (x < w - 1) Ver(a + 1);
                }
                if (r == 0) { r = pai.Count; pai.Add(r); }
                rot[o + x] = r;
            }
        }
        var novo = new int[pai.Count];
        var n = 0;
        for (var i = 1; i < pai.Count; i++) { var raiz = Raiz(i); if (raiz == i) novo[i] = ++n; }
        for (var i = 1; i < pai.Count; i++) novo[i] = novo[Raiz(i)];
        for (var i = 0; i < rot.Length; i++) if (rot[i] != 0) rot[i] = novo[rot[i]];
        return (rot, n);
    }

    public static void Rasterizar(Poligono pol, double x0, double y0, double dx, double dy, int w, int h, Action<int> marcar)
    {
        double minY = double.MaxValue, maxY = double.MinValue;
        foreach (var a in pol.Aneis)
            for (var i = 1; i < a.Length; i += 2) { if (a[i] < minY) minY = a[i]; if (a[i] > maxY) maxY = a[i]; }
        var l0 = Math.Max(0, (int)Math.Floor((y0 - maxY) / dy - 0.5));
        var l1 = Math.Min(h - 1, (int)Math.Ceiling((y0 - minY) / dy - 0.5));
        if (l0 > l1) return;
        var xs = new List<double>();
        for (var l = l0; l <= l1; l++)
        {
            var y = y0 - (l + 0.5) * dy;
            xs.Clear();
            foreach (var a in pol.Aneis)
            {
                var n = a.Length / 2;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double yi = a[i * 2 + 1], yj = a[j * 2 + 1];
                    if ((yi > y) != (yj > y))
                        xs.Add(a[i * 2] + (y - yi) / (yj - yi) * (a[j * 2] - a[i * 2]));
                }
            }
            if (xs.Count < 2) continue;
            xs.Sort();
            for (var k = 0; k + 1 < xs.Count; k += 2)
            {
                var c0 = Math.Max(0, (int)Math.Ceiling((xs[k] - x0) / dx - 0.5));
                var c1 = Math.Min(w - 1, (int)Math.Ceiling((xs[k + 1] - x0) / dx - 0.5) - 1);
                for (var c = c0; c <= c1; c++) marcar(l * w + c);
            }
        }
    }

    public static (double MinX, double MinY, double MaxX, double MaxY) Limites(IEnumerable<Poligono> pols)
    {
        double a = double.MaxValue, b = double.MaxValue, c = double.MinValue, d = double.MinValue;
        foreach (var p in pols)
            foreach (var r in p.Aneis)
                for (var i = 0; i + 1 < r.Length; i += 2)
                {
                    if (r[i] < a) a = r[i]; if (r[i] > c) c = r[i];
                    if (r[i + 1] < b) b = r[i + 1]; if (r[i + 1] > d) d = r[i + 1];
                }
        return (a, b, c, d);
    }
}
