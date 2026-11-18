using Servia.Domain.Appointments;

namespace Servia.Domain.Team;

/// <summary>
/// Elegibilidade de um profissional para receber agendamentos.
///
/// <b>Dois conceitos distintos, que não devem ser confundidos:</b>
///
/// <b>A. Ocupar vaga no plano SaaS</b> — <see cref="OccupiesPlanSlot"/>.
/// Responde "esta pessoa deve ser contabilizada no limite contratado?".
/// Depende de: estar ativa, ter ao menos um serviço ativo associado e ter
/// jornada configurada. <b>Não</b> depende de horário livre: um profissional
/// com a agenda cheia continua ocupando a vaga.
///
/// <b>B. Executar um serviço específico</b> — <see cref="CanExecute"/>.
/// Responde "esta pessoa pode receber este agendamento?". Depende do vínculo
/// com o serviço, da jornada, dos bloqueios e dos conflitos de horário.
///
/// A relação entre eles é de implicação parcial: quem executa um serviço
/// ocupa vaga, mas quem ocupa vaga pode estar indisponível em um horário
/// específico. Por isso a contagem do plano usa (A) e a agenda usa (B).
///
/// <b>Não varia por conflito transitório.</b> Um horário ocupado não torna o
/// profissional inelegível para a vaga. Se variasse, a contagem do plano
/// oscilaria a cada reserva e um cliente poderia "perder" uma vaga por ter
/// agenda cheia.
/// </summary>
public static class BookingEligibility
{
    /// <summary>
    /// Indica se o profissional está ativo e configurado para atender.
    ///
    /// Exige jornada com ao menos um dia de trabalho. A jornada é composição
    /// do profissional e sempre existe; a verificação protege contra dados
    /// materializados incompletos.
    /// </summary>
    public static bool IsOperationallyEligible(Professional professional)
    {
        ArgumentNullException.ThrowIfNull(professional);

        return professional.IsActive
            && professional.Schedule.WorkDays.Count > 0;
    }

    /// <summary>
    /// Indica se o profissional ocupa uma vaga no plano SaaS.
    ///
    /// Critério aprovado (ESCOPO_AV2 §2.3): ativo, com ao menos um serviço
    /// ativo associado e com jornada configurada. Não considera cargo,
    /// usuário vinculado, cadastro isolado nem disponibilidade momentânea.
    ///
    /// <paramref name="hasActiveLinkedService"/> informa se existe vínculo
    /// com um serviço ativo — a consulta pertence ao repositório, porque
    /// depende de dados fora do agregado do profissional.
    /// </summary>
    public static bool OccupiesPlanSlot(Professional professional, bool hasActiveLinkedService)
    {
        ArgumentNullException.ThrowIfNull(professional);

        return IsOperationallyEligible(professional) && hasActiveLinkedService;
    }

    /// <summary>
    /// Indica se o profissional pode executar o serviço informado.
    ///
    /// Exige que o profissional esteja operacionalmente elegível e que exista
    /// vínculo com um serviço ativo. O vínculo é o que autoriza o profissional
    /// a receber agendamentos daquele serviço.
    /// </summary>
    public static bool CanExecute(
        Professional professional,
        Servia.Domain.Catalog.Service service,
        bool isLinked)
    {
        ArgumentNullException.ThrowIfNull(professional);
        ArgumentNullException.ThrowIfNull(service);

        return IsOperationallyEligible(professional)
            && service.IsActive
            && isLinked;
    }
}