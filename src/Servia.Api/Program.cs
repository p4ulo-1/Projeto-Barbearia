using Servia.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

var app = builder.Build();

// O tratamento de exceções é o PRIMEIRO middleware do pipeline.
// Assim, qualquer exceção lançada adiante — controllers, serviços, domínio —
// é capturada e traduzida em resposta HTTP consistente.
app.UseExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

/// <summary>
/// Ponto de entrada exposto para testes de integração com WebApplicationFactory.
/// </summary>
public partial class Program;
