using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Team;

/// <summary>
/// Bloqueio pontual de agenda: folga, almoço, compromisso ou manutenção.
///
/// Um horário bloqueado não é oferecido como disponível, mesmo que esteja
/// dentro da jornada de trabalho. O bloqueio tem início e fim explícitos e
/// não pode ter duração zero ou negativa.
/// </summary>
public sealed class ScheduleBlock : ITenantScoped
{
    private ScheduleBlock(
        Guid id,
        Guid tenantId,
        Guid professionalId,
        DateTimeOffset start,
        DateTimeOffset end,
        string? reason)
    {
        Id = id;
        TenantId = tenantId;
        ProfessionalId = professionalId;
        Start = start;
        End = end;
        Reason = reason;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private ScheduleBlock()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProfessionalId { get; private set; }

    public DateTimeOffset Start { get; private set; }

    public DateTimeOffset End { get; private set; }

    public string? Reason { get; private set; }

    public static ScheduleBlock Create(
        Guid tenantId,
        Guid professionalId,
        DateTimeOffset start,
        DateTimeOffset end,
        string? reason = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (professionalId == Guid.Empty)
        {
            throw new DomainRuleException("O profissional é obrigatório.");
        }

        if (start >= end)
        {
            throw new DomainRuleException("O início do bloqueio deve ser anterior ao fim.");
        }

        return new ScheduleBlock(
            Guid.NewGuid(),
            tenantId,
            professionalId,
            start,
            end,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
    }

    /// <summary>
    /// Indica se o bloqueio se sobrepõe ao intervalo informado.
    /// </summary>
    public bool Overlaps(DateTimeOffset start, DateTimeOffset end) =>
        Start < end && End > start;

    /// <summary>Altera o motivo do bloqueio.</summary>
    public void ChangeReason(string? reason) =>
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
}
