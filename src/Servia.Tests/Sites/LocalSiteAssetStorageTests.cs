using System.Buffers.Binary;
using System.Text;
using Servia.Domain.Common;
using Servia.Domain.Sites;
using Servia.Infrastructure.Storage;

namespace Servia.Tests.Sites;

/// <summary>
/// Validação de conteúdo no armazenamento local (C4).
///
/// A política aceita apenas PNG e JPEG. Prova: imagens válidas passam;
/// extensão/MIME/assinatura incompatíveis, arquivos corrompidos, SVG
/// renomeado, formatos não permitidos e imagens acima do limite são
/// rejeitados — e nada é gravado.
/// </summary>
public sealed class LocalSiteAssetStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "servia-assets-" + Guid.NewGuid().ToString("N"));

    private readonly LocalSiteAssetStorage _storage;

    public LocalSiteAssetStorageTests()
    {
        _storage = new LocalSiteAssetStorage(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Png_Valido_E_Aceito()
    {
        var stored = await _storage.StoreAsync(
            Guid.NewGuid(), "logo.png", "image/png", new MemoryStream(BuildPng(100, 100)));

        Assert.Equal("image/png", stored.DetectedContentType);
    }

    [Fact]
    public async Task Jpeg_Valido_E_Aceito()
    {
        var stored = await _storage.StoreAsync(
            Guid.NewGuid(), "foto.jpg", "image/jpeg", new MemoryStream(BuildJpeg(200, 150)));

        Assert.Equal("image/jpeg", stored.DetectedContentType);
    }

    [Fact]
    public async Task Extensao_Incompativel_Com_Conteudo_E_Rejeitada()
    {
        // Conteudo PNG com extensao .jpg: a assinatura nao corresponde.
        await Assert.ThrowsAsync<DomainRuleException>(() =>
            _storage.StoreAsync(Guid.NewGuid(), "foto.jpg", "image/jpeg", new MemoryStream(BuildPng(10, 10))));
    }

    [Fact]
    public async Task Assinatura_Invalida_E_Rejeitada()
    {
        var text = Encoding.UTF8.GetBytes("isto nao e uma imagem");

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            _storage.StoreAsync(Guid.NewGuid(), "arquivo.png", "image/png", new MemoryStream(text)));
    }

    [Fact]
    public async Task Png_Corrompido_E_Rejeitado()
    {
        // Assinatura PNG valida, mas sem o chunk IHDR.
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            _storage.StoreAsync(Guid.NewGuid(), "logo.png", "image/png", new MemoryStream(bytes)));
    }

    [Fact]
    public async Task Svg_Renomeado_Para_Png_E_Rejeitado()
    {
        var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            _storage.StoreAsync(Guid.NewGuid(), "logo.png", "image/png", new MemoryStream(svg)));
    }

    [Fact]
    public async Task Gif_E_Rejeitado()
    {
        var gif = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00 };

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            _storage.StoreAsync(Guid.NewGuid(), "anim.gif", "image/gif", new MemoryStream(gif)));
    }

    [Fact]
    public async Task Webp_E_Rejeitado()
    {
        var webp = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            _storage.StoreAsync(Guid.NewGuid(), "foto.webp", "image/webp", new MemoryStream(webp)));
    }

    [Fact]
    public async Task Imagem_Acima_Do_Limite_De_Dimensoes_E_Rejeitada()
    {
        var huge = BuildPng(SiteAsset.MaxDimensionPixels + 1, 100);

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            _storage.StoreAsync(Guid.NewGuid(), "grande.png", "image/png", new MemoryStream(huge)));
    }

    [Fact]
    public async Task Arquivo_Acima_Do_Limite_De_Tamanho_E_Rejeitado_Sem_Bufferizar_Tudo()
    {
        var oversized = new EndlessStream(SiteAsset.MaxSizeBytes + 1024);

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            _storage.StoreAsync(Guid.NewGuid(), "grande.png", "image/png", oversized));

        Assert.True(oversized.BytesRead <= SiteAsset.MaxSizeBytes + 81920);
    }

    [Fact]
    public async Task Arquivo_Rejeitado_Nao_E_Gravado()
    {
        var text = Encoding.UTF8.GetBytes("nao e imagem");

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            _storage.StoreAsync(Guid.NewGuid(), "x.png", "image/png", new MemoryStream(text)));

        // Nenhum arquivo foi persistido.
        Assert.False(Directory.Exists(_root)
            && Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).Any());
    }

    // ---------- Construtores de imagens minimas validas ----------

    /// <summary>PNG minimo com assinatura + chunk IHDR (largura/altura).</summary>
    private static byte[] BuildPng(int width, int height)
    {
        var bytes = new byte[33];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);

        // Tamanho do chunk IHDR = 13.
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8, 4), 13);

        // "IHDR"
        bytes[12] = 0x49; bytes[13] = 0x48; bytes[14] = 0x44; bytes[15] = 0x52;

        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);

        // Profundidade, tipo de cor, compressao, filtro, entrelacamento.
        bytes[24] = 8; bytes[25] = 6; bytes[26] = 0; bytes[27] = 0; bytes[28] = 0;

        return bytes;
    }

    /// <summary>JPEG minimo com SOI + APP0 + SOF0 (largura/altura).</summary>
    private static byte[] BuildJpeg(int width, int height)
    {
        using var stream = new MemoryStream();

        // SOI
        stream.Write([0xFF, 0xD8]);

        // APP0 (JFIF) com tamanho 16.
        stream.Write([0xFF, 0xE0, 0x00, 0x10]);
        stream.Write("JFIF\0"u8);
        stream.Write([0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);

        // SOF0: marcador, tamanho 17, precisao 8, altura, largura, componentes.
        stream.Write([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        stream.Write([(byte)(height >> 8), (byte)(height & 0xFF)]);
        stream.Write([(byte)(width >> 8), (byte)(width & 0xFF)]);
        stream.Write([0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01]);

        return stream.ToArray();
    }

    /// <summary>Stream que gera bytes até um total, contando o que foi lido.</summary>
    private sealed class EndlessStream(long totalBytes) : Stream
    {
        private long _produced;

        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => totalBytes;

        public override long Position { get => _produced; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = totalBytes - _produced;

            if (remaining <= 0)
            {
                return 0;
            }

            var toRead = (int)Math.Min(count, remaining);
            Array.Fill(buffer, (byte)0x41, offset, toRead);
            _produced += toRead;
            BytesRead += toRead;

            return toRead;
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
