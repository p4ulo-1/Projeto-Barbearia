namespace BackAndre.Domain.Models;

public sealed class DomainRuleException(string message) : InvalidOperationException(message);
