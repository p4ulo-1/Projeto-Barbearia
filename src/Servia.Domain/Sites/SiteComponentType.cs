namespace Servia.Domain.Sites;

/// <summary>
/// Tipos de componente disponíveis na biblioteca do site (F3.2).
///
/// A IA <b>não</b> gera código: ela seleciona e configura componentes deste
/// catálogo fechado (ADR-007). Isso garante integração com agenda e
/// pagamentos, responsividade e consistência visual.
///
/// Adicionar um componente novo é uma decisão de produto, não de geração.
/// </summary>
public enum SiteComponentType
{
    /// <summary>Destaque principal com nome, chamada e botão de agendamento.</summary>
    Hero = 1,

    /// <summary>Apresentação do estabelecimento: história, diferenciais.</summary>
    About = 2,

    /// <summary>Lista de serviços com preço e duração.</summary>
    Services = 3,

    /// <summary>Apresentação dos profissionais com foto e especialidade.</summary>
    Team = 4,

    /// <summary>Galeria de trabalhos realizados.</summary>
    Gallery = 5,

    /// <summary>Depoimentos de clientes.</summary>
    Testimonials = 6,

    /// <summary>Endereço, telefone, horário e mapa.</summary>
    Contact = 7,

    /// <summary>Widget de agendamento integrado à agenda real (F3.8).</summary>
    BookingWidget = 8,

    /// <summary>Perguntas frequentes.</summary>
    Faq = 9,

    /// <summary>Chamada final para ação.</summary>
    CallToAction = 10
}
