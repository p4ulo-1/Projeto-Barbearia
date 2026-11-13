using System.Text.RegularExpressions;
using Servia.Domain.Common;

namespace Servia.Domain.Tenancy;

/// <summary>
/// Agregado raiz do estabelecimento.
///
/// Todo dado de negócio pertence a um tenant. O <see cref="Slug"/> é o
/// identificador usado no subdomínio e é <b>imutável</b> após a criação:
/// alterá-lo quebraria links publicados e o site do estabelecimento.
///
/// O ciclo de vida segue <see cref="TenantStatus"/>: <c>Trial</c> e
/// <c>Active</c> operam normalmente; <c>Suspended</c> bloqueia escrita;
/// <c>Cancelled</c> é terminal.
/// </summary>
public sealed partial class Tenant
{
    private Tenant(
        Guid id,
        string slug,
        string name,
        SegmentType segment,
        string document,
        TenantStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        Slug = slug;
        Name = name;
        Segment = segment;
        Document = document;
        Status = status;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private Tenant()
    {
        Slug = string.Empty;
        Name = string.Empty;
        Document = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Identificador do subdomínio. Imutável após a criação.</summary>
    public string Slug { get; private set; }

    public string Name { get; private set; }

    public SegmentType Segment { get; private set; }

    /// <summary>CNPJ ou CPF do responsável.</summary>
    public string Document { get; private set; }

    public TenantStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Expediente semanal do estabelecimento (independente dos profissionais).
    /// Nasce <b>fechado</b> até o administrador configurar.
    /// </summary>
    public EstablishmentSchedule Schedule { get; private set; } = EstablishmentSchedule.ClosedByDefault();

    /// <summary>Indica se o tenant pode escrever dados.</summary>
    public bool CanWrite => Status is TenantStatus.Trial or TenantStatus.Active;

    /// <summary>Indica se o tenant pode ler dados.</summary>
    public bool CanRead => Status != TenantStatus.Cancelled;

    /// <summary>Indica se o tenant está em período de teste.</summary>
    public bool IsOnTrial => Status == TenantStatus.Trial;

    public static Tenant Create(
        string slug,
        string name,
        SegmentType segment,
        string document,
        DateTimeOffset now,
        bool startOnTrial = true)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainRuleException("O identificador do estabelecimento é obrigatório.");
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();

        if (!SlugPattern().IsMatch(normalizedSlug))
        {
            throw new DomainRuleException(
                "O identificador deve conter apenas letras minúsculas, números e hífen, com 3 a 40 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(document))
        {
            throw new DomainRuleException("O documento do responsável é obrigatório.");
        }

        return new Tenant(
            Guid.NewGuid(),
            normalizedSlug,
            name.Trim(),
            segment,
            document.Trim(),
            startOnTrial ? TenantStatus.Trial : TenantStatus.Active,
            now);
    }

    /// <summary>Atualiza os dados cadastrais. O slug não é alterável.</summary>
    public void UpdateProfile(string name, SegmentType segment, string document)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(document))
        {
            throw new DomainRuleException("O documento do responsável é obrigatório.");
        }

        Name = name.Trim();
        Segment = segment;
        Document = document.Trim();
    }

    /// <summary>
    /// Atualiza o expediente do estabelecimento (administrador). O expediente
    /// não afeta os agendamentos — estes dependem só do profissional.
    /// </summary>
    public void UpdateSchedule(EstablishmentSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        Schedule = schedule;
    }

    /// <summary>Ativa o tenant ao fim do trial ou após regularização.</summary>
    public void Activate()
    {
        EnsureNotCancelled("Um estabelecimento cancelado não pode ser ativado.");

        Status = TenantStatus.Active;
    }

    /// <summary>Suspende o tenant por inadimplência.</summary>
    public void Suspend()
    {
        EnsureNotCancelled("Um estabelecimento cancelado não pode ser suspenso.");

        Status = TenantStatus.Suspended;
    }

    /// <summary>Encerra o tenant. Estado terminal.</summary>
    public void Cancel()
    {
        EnsureNotCancelled("O estabelecimento já está cancelado.");

        Status = TenantStatus.Cancelled;
    }

    private void EnsureNotCancelled(string message)
    {
        if (Status == TenantStatus.Cancelled)
        {
            throw new DomainRuleException(message);
        }
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$")]
    private static partial Regex SlugPattern();
}
