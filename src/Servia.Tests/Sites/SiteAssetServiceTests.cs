using Servia.Application.Sites;
using Servia.Domain.Common;
using Servia.Domain.Sites;
using Servia.Tests.Common;

namespace Servia.Tests.Sites;

/// <summary>
/// Serviço de arquivos do site com repositório dedicado (C11).
///
/// Prova que o upload persiste via <see cref="ISiteAssetRepository"/> e que
/// o download respeita o isolamento por tenant. A posse da sessão (C5) é
/// verificada pelo controller, coberta em teste de integração.
/// </summary>
public sealed class SiteAssetServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Upload_Persiste_Via_Repositorio_Dedicado()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeSiteAssetRepository();
        var storage = new FakeSiteAssetStorage();

        var service = new SiteAssetService(repository, storage, new FixedTimeProvider(Now));

        var asset = await service.UploadAsync(
            tenantId, Guid.NewGuid(), SiteAssetKind.Logo,
            "logo.png", "image/png", new MemoryStream([0x89, 0x50, 0x4E, 0x47]));

        Assert.Equal(tenantId, asset.TenantId);
        Assert.Same(asset, repository.LastAddedAsset);
        Assert.True(storage.StoreCalled);
    }

    [Fact]
    public async Task Open_Rejeita_Arquivo_De_Outro_Tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var repository = new FakeSiteAssetRepository();
        var storage = new FakeSiteAssetStorage();
        var service = new SiteAssetService(repository, storage, new FixedTimeProvider(Now));

        var asset = await service.UploadAsync(
            tenantA, null, SiteAssetKind.Logo,
            "logo.png", "image/png", new MemoryStream([0x89, 0x50, 0x4E, 0x47]));

        // Tenant B nao abre o arquivo de A.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.OpenAsync(tenantB, asset));
    }

    /// <summary>Repositório de arquivos em memória.</summary>
    private sealed class FakeSiteAssetRepository : ISiteAssetRepository
    {
        private readonly List<SiteAsset> _assets = [];

        public SiteAsset? LastAddedAsset { get; private set; }

        public Task<SiteAsset?> FindAsync(Guid tenantId, Guid assetId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_assets.FirstOrDefault(
                asset => asset.TenantId == tenantId && asset.Id == assetId));

        public Task<IReadOnlyList<SiteAsset>> ListBySessionAsync(
            Guid tenantId, Guid onboardingSessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SiteAsset>>(
                _assets.Where(asset => asset.TenantId == tenantId
                    && asset.OnboardingSessionId == onboardingSessionId).ToList());

        public Task AddAsync(SiteAsset asset, CancellationToken cancellationToken = default)
        {
            _assets.Add(asset);
            LastAddedAsset = asset;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    /// <summary>Armazenamento em memória que registra se foi chamado.</summary>
    private sealed class FakeSiteAssetStorage : ISiteAssetStorage
    {
        public bool StoreCalled { get; private set; }

        public Task<StoredAsset> StoreAsync(
            Guid tenantId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
        {
            StoreCalled = true;
            return Task.FromResult(new StoredAsset("path/logo.png", 4, "checksum", "image/png"));
        }

        public Task<Stream> OpenReadAsync(Guid tenantId, string storagePath, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task DeleteAsync(Guid tenantId, string storagePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
