using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Servia.Api.Contracts;

/// <summary>
/// Parâmetros de paginação (F13.10).
///
/// Listagens grandes nunca retornam tudo de uma vez: o cliente pede uma
/// página e recebe o total, para montar a navegação.
///
/// O tamanho máximo é limitado no servidor: um cliente não pode pedir
/// 100.000 registros e derrubar a API.
/// </summary>
public sealed class PageRequest
{
    /// <summary>Tamanho máximo de página aceito pelo servidor.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Tamanho padrão quando o cliente não informa.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Página solicitada, começando em 1.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior que zero.")]
    public int Page { get; set; } = 1;

    /// <summary>Itens por página.</summary>
    [Range(1, MaxPageSize, ErrorMessage = "O tamanho da página deve estar entre 1 e 100.")]
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Quantidade de itens a pular. Calculada, nao vinculada.</summary>
    [JsonIgnore]
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Normaliza os parametros recebidos da query string.
    /// Valores fora do intervalo sao corrigidos, nao rejeitados: uma
    /// listagem com pagina invalida deve devolver a primeira pagina, nao
    /// um erro que impede o cliente de ver os dados.
    /// </summary>
    public static PageRequest Normalize(int page, int pageSize) => new()
    {
        Page = page < 1 ? 1 : page,
        PageSize = pageSize < 1 ? DefaultPageSize
            : pageSize > MaxPageSize ? MaxPageSize : pageSize
    };
}

/// <summary>Resultado paginado.</summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    /// <summary>Total de páginas disponíveis.</summary>
    public int TotalPages => PageSize == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Indica se há página anterior.</summary>
    public bool HasPrevious => Page > 1;

    /// <summary>Indica se há próxima página.</summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>Monta a resposta a partir dos itens da página e do total.</summary>
    public static PagedResponse<T> Create(
        IReadOnlyList<T> items,
        PageRequest request,
        int totalCount) =>
        new(items, request.Page, request.PageSize, totalCount);
}
