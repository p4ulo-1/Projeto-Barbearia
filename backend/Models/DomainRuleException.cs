namespace BackAndre.Models;

public sealed class DomainRuleException(string message) : InvalidOperationException(message);
