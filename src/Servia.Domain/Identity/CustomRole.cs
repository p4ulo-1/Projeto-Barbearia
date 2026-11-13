using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Identity;

/// <summary>
/// Papel personalizado criado pelo Owner para organizar a equipe.
///
/// Pertence ao tenant que o criou e agrupa um conjunto de permissões
/// escolhidas individualmente. Substitui os títulos predefinidos quando o
/// estabelecimento precisa de uma combinação própria de acessos.
///
/// <b>Limites:</b> não pode conceder permissões exclusivas do Owner
/// (<see cref="Permission.ManageBilling"/>) nem da plataforma
/// (<see cref="Permission.ManagePlatformTenants"/>,
/// <see cref="Permission.ManagePlatformPlans"/>). A validação vive em
/// <see cref="RolePermissions.EnsureAssignableToCustomRole"/>.
/// </summary>
public sealed class CustomRole : ITenantScoped
{
    // Reatribuído (nunca mutado no lugar) para que o EF Core detecte a
    // alteração do conjunto e persista a nova lista de permissões.
    private HashSet<Permission> _permissions = [];

    private CustomRole(Guid id, Guid tenantId, string name)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private CustomRole()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Permissões concedidas pelo papel.</summary>
    public IReadOnlySet<Permission> Permissions => _permissions;

    public static CustomRole Create(Guid tenantId, string name, IEnumerable<Permission> permissions)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        var role = new CustomRole(Guid.NewGuid(), tenantId, NormalizeName(name));

        role.ReplacePermissions(permissions);

        return role;
    }

    /// <summary>Altera o nome do papel.</summary>
    public void Rename(string name) => Name = NormalizeName(name);

    /// <summary>
    /// Substitui o conjunto de permissões. Rejeita permissões exclusivas do
    /// Owner ou da plataforma.
    /// </summary>
    public void ReplacePermissions(IEnumerable<Permission> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var next = new HashSet<Permission>();

        foreach (var permission in permissions)
        {
            RolePermissions.EnsureAssignableToCustomRole(permission);
            next.Add(permission);
        }

        // Reatribui a referência: o EF Core compara o valor da propriedade
        // e detecta a alteração (mutação no lugar não seria detectada).
        _permissions = next;
    }

    /// <summary>Indica se o papel concede a permissão informada.</summary>
    public bool Has(Permission permission) => _permissions.Contains(permission);

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do papel é obrigatório.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length > 100)
        {
            throw new DomainRuleException("O nome do papel deve ter no máximo 100 caracteres.");
        }

        return trimmed;
    }
}
