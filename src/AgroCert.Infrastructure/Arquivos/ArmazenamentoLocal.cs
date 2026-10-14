using AgroCert.Application.Interfaces;
using AgroCert.Domain.Excecoes;
using AgroCert.Infrastructure.Integracoes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AgroCert.Infrastructure.Arquivos;

public class ArmazenamentoLocal : IArmazenamentoArquivos
{
    public const string PrefixoUrl = "/uploads/";

    private static readonly HashSet<string> ExtensoesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
    };

    private readonly OpcoesUploads _opcoes;
    public string DiretorioBase { get; }

    public ArmazenamentoLocal(IOptions<OpcoesAgroCert> opcoes, IHostEnvironment ambiente)
    {
        _opcoes = opcoes.Value.Uploads;
        DiretorioBase = Path.GetFullPath(Path.IsPathRooted(_opcoes.Diretorio)
            ? _opcoes.Diretorio
            : Path.Combine(ambiente.ContentRootPath, _opcoes.Diretorio));
        Directory.CreateDirectory(DiretorioBase);
    }

    public async Task<string?> SalvarAsync(ArquivoEnviado? arquivo, string subpasta, CancellationToken ct = default)
    {
        if (arquivo is null || arquivo.Tamanho <= 0) return null;

        var extensao = Path.GetExtension(arquivo.NomeArquivo);
        if (!ExtensoesPermitidas.Contains(extensao))
            throw new RegraNegocioException($"Tipo de arquivo não permitido ({extensao}). Envie uma imagem (JPG, PNG, GIF, WEBP).");
        if (arquivo.Tamanho > _opcoes.TamanhoMaximoMb * 1024L * 1024L)
            throw new RegraNegocioException($"A imagem passa do limite de {_opcoes.TamanhoMaximoMb} MB.");

        try
        {
            var pasta = Path.Combine(DiretorioBase, subpasta);
            Directory.CreateDirectory(pasta);
            var nome = Guid.NewGuid() + extensao.ToLowerInvariant();
            await using var destino = File.Create(Path.Combine(pasta, nome));
            await arquivo.Conteudo.CopyToAsync(destino, ct);
            return PrefixoUrl + subpasta + "/" + nome;
        }
        catch (IOException e)
        {
            throw new RegraNegocioException("Não foi possível salvar o arquivo enviado: " + e.Message);
        }
    }

    public async Task<string?> SalvarBytesAsync(byte[]? conteudo, string subpasta, string extensao, CancellationToken ct = default)
    {
        if (conteudo is null || conteudo.Length == 0) return null;
        try
        {
            var pasta = Path.Combine(DiretorioBase, subpasta);
            Directory.CreateDirectory(pasta);
            var nome = Guid.NewGuid() + "." + extensao.TrimStart('.');
            await File.WriteAllBytesAsync(Path.Combine(pasta, nome), conteudo, ct);
            return PrefixoUrl + subpasta + "/" + nome;
        }
        catch (IOException e)
        {
            throw new RegraNegocioException("Não foi possível salvar a imagem gerada: " + e.Message);
        }
    }

    public async Task<byte[]?> LerAsync(string url, CancellationToken ct = default)
    {
        var caminho = CaminhoFisico(url);
        return caminho != null && File.Exists(caminho) ? await File.ReadAllBytesAsync(caminho, ct) : null;
    }

    public string? CaminhoFisico(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var relativo = url.StartsWith(PrefixoUrl, StringComparison.Ordinal) ? url[PrefixoUrl.Length..] : url.TrimStart('/');
        var completo = Path.GetFullPath(Path.Combine(DiretorioBase, relativo));
        return completo.StartsWith(DiretorioBase + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? completo : null;
    }
}
