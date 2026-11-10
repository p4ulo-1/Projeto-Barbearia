namespace Servia.Domain.Common;

/// <summary>
/// Objeto de valor que representa um valor monetário com moeda.
/// Centraliza a invariante de valor não negativo e evita ambiguidade de moeda.
/// </summary>
public readonly record struct Money
{
    public const string DefaultCurrency = "BRL";

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public static Money From(decimal amount, string currency = DefaultCurrency)
    {
        if (amount < 0)
        {
            throw new DomainRuleException("O valor monetário não pode ser negativo.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new DomainRuleException("A moeda é obrigatória.");
        }

        return new Money(amount, currency.Trim().ToUpperInvariant());
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);

        if (other.Amount > Amount)
        {
            throw new DomainRuleException("A subtração resultaria em valor negativo.");
        }

        return new Money(Amount - other.Amount, Currency);
    }

    public Money MultiplyBy(decimal factor)
    {
        if (factor < 0)
        {
            throw new DomainRuleException("O fator de multiplicação não pode ser negativo.");
        }

        return new Money(Amount * factor, Currency);
    }

    /// <summary>
    /// Divide o valor por um divisor, arredondando para 2 casas decimais.
    /// O arredondamento e necessario porque valores monetarios nao tem
    /// fracoes de centavo: dividir 300 por 3 deve resultar em 100, nao em
    /// 99.999... como ocorre ao multiplicar pelo inverso.
    /// </summary>
    public Money DivideBy(int divisor)
    {
        if (divisor <= 0)
        {
            throw new DomainRuleException("O divisor deve ser maior que zero.");
        }

        var result = Math.Round(Amount / divisor, 2, MidpointRounding.AwayFromZero);

        return new Money(result, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new DomainRuleException(
                $"Não é possível operar valores de moedas diferentes ({Currency} e {other.Currency}).");
        }
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}
