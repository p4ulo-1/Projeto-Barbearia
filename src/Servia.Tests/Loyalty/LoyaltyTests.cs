using Servia.Domain.Common;
using Servia.Domain.Loyalty;

namespace Servia.Tests.Loyalty;

/// <summary>
/// F7.5 — programa de fidelidade.
/// F7.6 — recuperação de clientes inativos.
/// F7.7 — avaliações e NPS.
/// </summary>
public class LoyaltyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid AppointmentId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 10);
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    // ---------- F7.5: fidelidade ----------

    [Fact]
    public void Abrir_Conta_Inicia_Com_Zero_Pontos()
    {
        var account = LoyaltyAccount.Open(TenantId, CustomerId);

        Assert.Equal(0, account.Points);
        Assert.Equal(CustomerId, account.CustomerId);
    }

    [Fact]
    public void Creditar_Pontos_Por_Atendimento()
    {
        var account = LoyaltyAccount.Open(TenantId, CustomerId);

        account.Earn(50);

        Assert.Equal(50, account.Points);
        Assert.Equal(50, account.TotalEarned);
    }

    [Fact]
    public void Resgatar_Pontos_Reduz_Saldo()
    {
        var account = LoyaltyAccount.Open(TenantId, CustomerId);
        account.Earn(100);

        account.Redeem(40);

        Assert.Equal(60, account.Points);
        Assert.Equal(40, account.TotalRedeemed);
    }

    [Fact]
    public void Resgatar_Alem_Do_Saldo_Lanca_Excecao()
    {
        var account = LoyaltyAccount.Open(TenantId, CustomerId);
        account.Earn(30);

        Assert.Throws<DomainRuleException>(() => account.Redeem(50));
    }

    [Fact]
    public void Creditar_Pontos_Invalidos_Lanca_Excecao()
    {
        var account = LoyaltyAccount.Open(TenantId, CustomerId);

        Assert.Throws<DomainRuleException>(() => account.Earn(0));
    }

    [Fact]
    public void CanRedeem_Reflete_O_Saldo()
    {
        var account = LoyaltyAccount.Open(TenantId, CustomerId);
        account.Earn(50);

        Assert.True(account.CanRedeem(50));
        Assert.False(account.CanRedeem(51));
    }

    // ---------- F7.6: recuperacao de inativos ----------

    [Fact]
    public void Cliente_Sem_Visita_Recente_Eh_Inativo()
    {
        var campaign = ReactivationCampaign.Create(TenantId, "Volte a nos visitar", 60);

        Assert.True(campaign.IsInactive(Today.AddDays(-90), Today));
    }

    [Fact]
    public void Cliente_Com_Visita_Recente_Nao_Eh_Inativo()
    {
        var campaign = ReactivationCampaign.Create(TenantId, "Campanha", 60);

        Assert.False(campaign.IsInactive(Today.AddDays(-10), Today));
    }

    [Fact]
    public void Cliente_Que_Nunca_Veio_Eh_Inativo()
    {
        var campaign = ReactivationCampaign.Create(TenantId, "Campanha", 60);

        Assert.True(campaign.IsInactive(null, Today));
    }

    [Fact]
    public void Limite_Exato_De_Inatividade()
    {
        var campaign = ReactivationCampaign.Create(TenantId, "Campanha", 60);

        Assert.False(campaign.IsInactive(Today.AddDays(-59), Today));
        Assert.True(campaign.IsInactive(Today.AddDays(-60), Today));
    }

    [Fact]
    public void Campanha_Exige_Consentimento()
    {
        var campaign = ReactivationCampaign.Create(TenantId, "Campanha", 60);
        var lastVisit = Today.AddDays(-90);

        Assert.True(campaign.ShouldTarget(lastVisit, Today, hasConsent: true));
        Assert.False(campaign.ShouldTarget(lastVisit, Today, hasConsent: false));
    }

    [Fact]
    public void Campanha_Inativa_Nao_Dispara()
    {
        var campaign = ReactivationCampaign.Create(TenantId, "Campanha", 60);
        campaign.Deactivate();

        Assert.False(campaign.ShouldTarget(Today.AddDays(-90), Today, hasConsent: true));
    }

    // ---------- F7.7: NPS ----------

    [Theory]
    [InlineData(10, NpsCategory.Promoter)]
    [InlineData(9, NpsCategory.Promoter)]
    [InlineData(8, NpsCategory.Passive)]
    [InlineData(7, NpsCategory.Passive)]
    [InlineData(6, NpsCategory.Detractor)]
    [InlineData(0, NpsCategory.Detractor)]
    public void Classificar_Nota_Em_Categoria_Nps(int score, NpsCategory expected)
    {
        Assert.Equal(expected, ServiceReview.Classify(score));
    }

    [Fact]
    public void Criar_Avaliacao_Valida()
    {
        var review = ServiceReview.Create(
            TenantId, AppointmentId, CustomerId, 9, Now, "Excelente atendimento");

        Assert.Equal(9, review.Score);
        Assert.Equal(NpsCategory.Promoter, review.Category);
        Assert.Equal("Excelente atendimento", review.Comment);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Nota_Fora_Do_Intervalo_Lanca_Excecao(int score)
    {
        Assert.Throws<DomainRuleException>(() =>
            ServiceReview.Create(TenantId, AppointmentId, CustomerId, score, Now));
    }

    [Fact]
    public void Avaliacao_Sem_Atendimento_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            ServiceReview.Create(TenantId, Guid.Empty, CustomerId, 9, Now));
    }

    [Fact]
    public void Nps_Calcula_Promotores_Menos_Detratores()
    {
        // 6 promotores, 2 neutros, 2 detratores: 60% - 20% = 40
        var reviews = new List<ServiceReview>();
        for (var index = 0; index < 6; index++)
        {
            reviews.Add(ServiceReview.Create(TenantId, Guid.NewGuid(), CustomerId, 10, Now));
        }

        for (var index = 0; index < 2; index++)
        {
            reviews.Add(ServiceReview.Create(TenantId, Guid.NewGuid(), CustomerId, 8, Now));
        }

        for (var index = 0; index < 2; index++)
        {
            reviews.Add(ServiceReview.Create(TenantId, Guid.NewGuid(), CustomerId, 3, Now));
        }

        var report = NpsReport.Build(reviews);

        Assert.Equal(10, report.TotalResponses);
        Assert.Equal(6, report.Promoters);
        Assert.Equal(2, report.Passives);
        Assert.Equal(2, report.Detractors);
        Assert.Equal(40m, report.Score);
    }

    [Fact]
    public void Nps_Sem_Respostas_Eh_Zero()
    {
        var report = NpsReport.Build([]);

        Assert.Equal(0, report.TotalResponses);
        Assert.Equal(0m, report.Score);
    }

    [Fact]
    public void Nps_Pode_Ser_Negativo()
    {
        // 1 promotor, 9 detratores: 10% - 90% = -80
        var reviews = new List<ServiceReview>
        {
            ServiceReview.Create(TenantId, Guid.NewGuid(), CustomerId, 10, Now)
        };

        for (var index = 0; index < 9; index++)
        {
            reviews.Add(ServiceReview.Create(TenantId, Guid.NewGuid(), CustomerId, 2, Now));
        }

        Assert.Equal(-80m, NpsReport.Build(reviews).Score);
    }
}
