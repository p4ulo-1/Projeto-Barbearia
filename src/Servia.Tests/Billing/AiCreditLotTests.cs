using Servia.Domain.Billing;
using Servia.Domain.Common;

namespace Servia.Tests.Billing;

/// <summary>
/// Lotes de créditos adicionais de IA (gestão de IA, §7.1).
///
/// Cada aquisição é um lote independente, com validade própria. Comprar um
/// pacote novo não renova a validade dos anteriores, e créditos expirados
/// não podem ser consumidos.
/// </summary>
public class AiCreditLotTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static AiCreditLot NewLot(int quantity = 10, int daysToExpire = 45) =>
        AiCreditLot.Create(
            TenantId,
            quantity,
            Money.From(quantity * 0.03m),
            discountPercent: 0m,
            acquiredAt: Now,
            expiresAt: Now.AddDays(daysToExpire));

    // ---------- Criação ----------

    [Fact]
    public void Cria_Lote_Com_Saldo_Integral()
    {
        var lot = NewLot(quantity: 50);

        Assert.Equal(50, lot.Quantity);
        Assert.Equal(0, lot.Consumed);
        Assert.Equal(50, lot.Remaining);
        Assert.True(lot.HasBalance);
    }

    [Fact]
    public void Rejeita_Quantidade_Nao_Positiva()
    {
        Assert.Throws<DomainRuleException>(() => NewLot(quantity: 0));
    }

    [Fact]
    public void Rejeita_Expiracao_Anterior_A_Aquisicao()
    {
        Assert.Throws<DomainRuleException>(() =>
            AiCreditLot.Create(
                TenantId, 10, Money.From(0.30m), 0m, Now, Now.AddDays(-1)));
    }

    [Fact]
    public void Rejeita_Desconto_Fora_Da_Faixa()
    {
        Assert.Throws<DomainRuleException>(() =>
            AiCreditLot.Create(
                TenantId, 10, Money.From(0.30m), 150m, Now, Now.AddDays(45)));
    }

    // ---------- Consumo ----------

    [Fact]
    public void Consome_Ate_O_Saldo_Disponivel()
    {
        var lot = NewLot(quantity: 10);

        var consumed = lot.Consume(4, Now);

        Assert.Equal(4, consumed);
        Assert.Equal(6, lot.Remaining);
    }

    [Fact]
    public void Consumo_Nao_Ultrapassa_O_Saldo()
    {
        var lot = NewLot(quantity: 3);

        var consumed = lot.Consume(10, Now);

        Assert.Equal(3, consumed);
        Assert.Equal(0, lot.Remaining);
        Assert.False(lot.HasBalance);
    }

    [Fact]
    public void Lote_Expirado_Nao_Pode_Ser_Consumido()
    {
        var lot = NewLot(quantity: 10, daysToExpire: 1);

        Assert.True(lot.IsExpired(Now.AddDays(2)));
        Assert.False(lot.IsConsumable(Now.AddDays(2)));

        Assert.Throws<DomainRuleException>(() => lot.Consume(1, Now.AddDays(2)));
    }

    [Fact]
    public void Lote_Vigente_Eh_Consumivel()
    {
        var lot = NewLot(quantity: 10, daysToExpire: 45);

        Assert.False(lot.IsExpired(Now));
        Assert.True(lot.IsConsumable(Now));
    }

    [Fact]
    public void Lote_Sem_Saldo_Nao_Eh_Consumivel()
    {
        var lot = NewLot(quantity: 2);
        lot.Consume(2, Now);

        Assert.False(lot.IsConsumable(Now));
    }

    // ---------- Independência dos lotes ----------

    [Fact]
    public void Lotes_Independentes_Preservam_Suas_Validades()
    {
        var first = NewLot(quantity: 10, daysToExpire: 45);
        var second = NewLot(quantity: 50, daysToExpire: 70);

        // Comprar o segundo lote não altera a validade do primeiro.
        Assert.Equal(Now.AddDays(45), first.ExpiresAt);
        Assert.Equal(Now.AddDays(70), second.ExpiresAt);

        first.Consume(10, Now);

        Assert.Equal(0, first.Remaining);
        Assert.Equal(50, second.Remaining);
    }
}
