namespace Servia.Domain.Tenancy;

/// <summary>
/// Segmento de atuação do estabelecimento.
///
/// O núcleo do sistema <b>não</b> consulta este valor para decidir
/// comportamento (ADR-018). Ele serve para configuração, apresentação e para
/// escolher qual política injetar na composição da aplicação.
/// </summary>
public enum SegmentType
{
    Barbershop = 1,
    HairSalon = 2,
    NailStudio = 3,
    Aesthetics = 4,
    BrowsAndLashes = 5,
    Tattoo = 6,
    Massage = 7,
    Makeup = 8,
    Waxing = 9,
    Braids = 10,
    Other = 99
}
