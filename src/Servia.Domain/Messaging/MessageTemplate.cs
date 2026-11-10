using System.Text.RegularExpressions;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Messaging;

/// <summary>
/// Template de mensagem editável por tenant (F6.2).
///
/// O corpo usa variáveis no formato <c>{{nome}}</c>. A renderização falha se
/// uma variável usada no corpo não for fornecida — melhor falhar do que
/// enviar ao cliente uma mensagem com <c>{{cliente}}</c> no lugar do nome.
/// </summary>
public sealed partial class MessageTemplate : ITenantScoped
{
    private MessageTemplate(
        Guid id,
        Guid tenantId,
        string key,
        MessageChannel channel,
        MessageType messageType,
        string body,
        bool isActive)
    {
        Id = id;
        TenantId = tenantId;
        Key = key;
        Channel = channel;
        MessageType = messageType;
        Body = body;
        IsActive = isActive;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private MessageTemplate()
    {
        Key = string.Empty;
        Body = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Identificador do template dentro do tenant.</summary>
    public string Key { get; private set; }

    public MessageChannel Channel { get; private set; }

    public MessageType MessageType { get; private set; }

    /// <summary>Corpo com variáveis no formato <c>{{nome}}</c>.</summary>
    public string Body { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Variáveis usadas no corpo do template.</summary>
    public IReadOnlyCollection<string> Variables =>
        VariablePattern().Matches(Body)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static MessageTemplate Create(
        Guid tenantId,
        string key,
        MessageChannel channel,
        MessageType messageType,
        string body)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainRuleException("A chave do template é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DomainRuleException("O corpo do template é obrigatório.");
        }

        return new MessageTemplate(
            Guid.NewGuid(), tenantId, key.Trim(), channel, messageType, body.Trim(), isActive: true);
    }

    /// <summary>
    /// Renderiza o corpo substituindo as variáveis pelos valores fornecidos.
    /// Falha se alguma variável usada no corpo não for fornecida.
    /// </summary>
    public string Render(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var missing = Variables
            .Where(variable => !values.ContainsKey(variable))
            .ToArray();

        if (missing.Length > 0)
        {
            throw new DomainRuleException(
                $"Variáveis não fornecidas: {string.Join(", ", missing)}.");
        }

        return VariablePattern().Replace(Body, match =>
        {
            var variable = match.Groups[1].Value;

            return values.TryGetValue(variable, out var value) ? value : match.Value;
        });
    }

    /// <summary>Atualiza o corpo do template.</summary>
    public void UpdateBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DomainRuleException("O corpo do template é obrigatório.");
        }

        Body = body.Trim();
    }

    /// <summary>Desativa o template, impedindo novos envios.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reativa o template.</summary>
    public void Activate() => IsActive = true;

    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex VariablePattern();
}
