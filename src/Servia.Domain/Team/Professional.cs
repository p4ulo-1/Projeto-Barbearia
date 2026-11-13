using Servia.Domain.Appointments;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Team;

/// <summary>
/// Profissional que executa serviços em um estabelecimento.
///
/// Mantém a jornada de trabalho como <b>composição</b>: o <see cref="WorkSchedule"/>
/// não existe sem o profissional e não é compartilhado entre eles. Por isso o
/// construtor é privado e a jornada só pode ser trocada por <see cref="UpdateSchedule"/>,
/// que valida a nova jornada antes de aceitá-la.
/// </summary>
public sealed class Professional : ITenantScoped
{
    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>,
    /// que é o único caminho público de construção.
    /// </summary>
    private Professional()
    {
        Name = string.Empty;
        Schedule = null!;
    }

    private Professional(
        Guid id,
        Guid tenantId,
        Guid? userId,
        string name,
        string? title,
        string? bio,
        string? photoUrl,
        WorkSchedule schedule,
        bool isActive,
        bool isServiceProvider)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Name = name;
        Title = title;
        Bio = bio;
        PhotoUrl = photoUrl;
        Schedule = schedule;
        IsActive = isActive;
        IsServiceProvider = isServiceProvider;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Vínculo opcional com o usuário de login do profissional.</summary>
    public Guid? UserId { get; private set; }

    public string Name { get; private set; }

    public string? Title { get; private set; }

    public string? Bio { get; private set; }

    public string? PhotoUrl { get; private set; }

    /// <summary>Jornada de trabalho. Parte do ciclo de vida do profissional.</summary>
    public WorkSchedule Schedule { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Indica se a pessoa presta serviços e pode receber agendamentos.
    ///
    /// <b>Independe do perfil de acesso:</b> um administrador ou recepcionista
    /// que também atende é um prestador; um profissional cadastrado que não
    /// atende não é. Apenas prestadores ativos ocupam vaga no plano.
    /// </summary>
    public bool IsServiceProvider { get; private set; }

    /// <summary>
    /// Indica se o profissional ocupa uma vaga no plano: precisa estar ativo
    /// e prestar serviços.
    /// </summary>
    public bool OccupiesPlanSlot => IsActive && IsServiceProvider;

    public static Professional Create(
        Guid tenantId,
        string name,
        WorkSchedule schedule,
        string? title = null,
        string? bio = null,
        string? photoUrl = null,
        Guid? userId = null,
        bool isServiceProvider = true)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do profissional é obrigatório.");
        }

        ArgumentNullException.ThrowIfNull(schedule);

        return new Professional(
            Guid.NewGuid(),
            tenantId,
            userId,
            name.Trim(),
            Normalize(title),
            Normalize(bio),
            Normalize(photoUrl),
            schedule,
            isActive: true,
            isServiceProvider);
    }

    /// <summary>
    /// Define se a pessoa presta serviços. Ao passar a prestar, passa a
    /// ocupar vaga no plano; ao deixar de prestar, a vaga é liberada.
    /// A validação do limite do plano é responsabilidade da camada de
    /// aplicação, que conhece a assinatura vigente.
    /// </summary>
    public void SetServiceProvider(bool isServiceProvider) =>
        IsServiceProvider = isServiceProvider;

    /// <summary>Atualiza os dados de apresentação do profissional.</summary>
    public void UpdateProfile(string name, string? title, string? bio, string? photoUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do profissional é obrigatório.");
        }

        Name = name.Trim();
        Title = Normalize(title);
        Bio = Normalize(bio);
        PhotoUrl = Normalize(photoUrl);
    }

    /// <summary>
    /// Substitui a jornada de trabalho.
    /// A jornada recebida já passou pelas invariantes de <see cref="WorkSchedule"/>.
    /// </summary>
    public void UpdateSchedule(WorkSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        Schedule = schedule;
    }

    /// <summary>Vincula o profissional a um usuário de login.</summary>
    public void LinkUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainRuleException("O usuário é obrigatório.");
        }

        UserId = userId;
    }

    /// <summary>Desativa o profissional, preservando o histórico de atendimentos.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reativa o profissional.</summary>
    public void Activate() => IsActive = true;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
