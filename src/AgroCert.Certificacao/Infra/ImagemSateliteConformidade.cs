using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AgroCert.Application.Interfaces;
using AgroCert.Application.Servicos;
using AgroCert.Application.Util;
using AgroCert.Infrastructure.Arquivos;
using Microsoft.Extensions.Logging;

namespace AgroCert.Certificacao.Infra;

public class ImagemSateliteConformidade
{
    private readonly MonitoramentoAmbientalServico _servico;
    private readonly ICopernicusCliente _copernicus;
    private readonly ArmazenamentoLocal _armazenamento;
    private readonly ILogger<ImagemSateliteConformidade> _log;

    public ImagemSateliteConformidade(MonitoramentoAmbientalServico servico, ICopernicusCliente copernicus,
        ArmazenamentoLocal armazenamento, ILogger<ImagemSateliteConformidade> log)
    {
        _servico = servico;
        _copernicus = copernicus;
        _armazenamento = armazenamento;
        _log = log;
    }

    public async Task<(string? Arquivo, int Status)> ObterAsync(long id, string? tipo, int ano, string? caixa, CancellationToken ct)
    {
        var hoje = DateTime.Today;
        if (tipo is not ("cor" or "ndvi") || ano < 2015 || ano > hoje.Year || string.IsNullOrEmpty(caixa)) return (null, 400);
        var partes = caixa.Split(',');
        if (partes.Length != 4) return (null, 400);
        var c = new double[4];
        for (var i = 0; i < 4; i++)
            if (!double.TryParse(partes[i], NumberStyles.Float, CultureInfo.InvariantCulture, out c[i])) return (null, 400);
        if (c[2] <= c[0] || c[3] <= c[1] || c[2] - c[0] > 0.6 || c[3] - c[1] > 0.6) return (null, 400);
        if (!_copernicus.Configurado) return (null, 404);

        // só retângulos perto do próprio imóvel (evita uso da cota para qualquer lugar)
        var p = await _servico.ObterPropriedadeAsync(id, ct);
        var bb = GeoUtil.BoundingBox(p.GeoJson);
        if (bb is null || c[0] > bb[2] + 0.1 || c[2] < bb[0] - 0.1 || c[1] > bb[3] + 0.1 || c[3] < bb[1] - 0.1) return (null, 400);

        var chave = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(
            $"{tipo}|{ano}|{string.Join(",", c.Select(v => v.ToString("0.000000", CultureInfo.InvariantCulture)))}"))).ToLowerInvariant();
        var pasta = Path.Combine(_armazenamento.DiretorioBase, "conformidade");
        var arquivo = Path.Combine(pasta, chave + ".png");
        if (File.Exists(arquivo)) return (arquivo, 200);

        var lat = (c[1] + c[3]) / 2;
        var largM = (c[2] - c[0]) * 111_320 * Math.Cos(lat * Math.PI / 180);
        var altM = (c[3] - c[1]) * 110_574;
        var lado = Math.Clamp(Math.Max(largM, altM) / 10, 256, 1024);
        var (w, h) = largM >= altM ? ((int)lado, (int)Math.Round(lado * altM / largM)) : ((int)Math.Round(lado * largM / altM), (int)lado);
        var desde = new DateOnly(ano, 6, 1);
        var ate = new DateOnly(ano, 9, 30);
        if (ate > DateOnly.FromDateTime(hoje)) ate = DateOnly.FromDateTime(hoje);
        try
        {
            var png = await _copernicus.GerarImagemRetanguloAsync(c, desde, ate, tipo, w, h, ct);
            Directory.CreateDirectory(pasta);
            await File.WriteAllBytesAsync(arquivo, png, ct);
            return (arquivo, 200);
        }
        catch (Exception e)
        {
            _log.LogWarning("Sentinel-2 para o relatório de conformidade ({Tipo} {Ano}): {Erro}", tipo, ano, e.Message);
            return (null, 502);
        }
    }
}
