namespace Servia.Domain.Identity;

/// <summary>
/// Papel de um usuário dentro de um estabelecimento.
///
/// Os papéis são <b>por tenant</b> (ADR-015): a mesma pessoa pode ser
/// proprietária em um estabelecimento e cliente em outro, com registros
/// independentes e sem vínculo entre eles.
///
/// <see cref="PlatformAdmin"/> é a exceção: pertence à plataforma, não a um
/// tenant, e tem privilégios distintos das permissões de estabelecimento
/// (F1.13).
/// </summary>
public enum Role
{
    /// <summary>Administrador da plataforma. Não pertence a um tenant.</summary>
    PlatformAdmin = 0,

    /// <summary>Proprietário do estabelecimento. Acesso total ao tenant.</summary>
    Owner = 1,

    /// <summary>Administrador do estabelecimento. Acesso operacional amplo.</summary>
    Administrator = 2,

    /// <summary>Profissional que executa serviços.</summary>
    Professional = 3,

    /// <summary>Recepcionista: agenda e atendimento ao cliente.</summary>
    Receptionist = 4,

    /// <summary>Cliente final do estabelecimento.</summary>
    Customer = 5
}
