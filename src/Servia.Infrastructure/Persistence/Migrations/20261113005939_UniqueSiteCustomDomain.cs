using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueSiteCustomDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normaliza dominios existentes para minusculas antes de criar o
            // indice, de modo que variacoes de caixa nao gerem duplicidade.
            // Se a normalizacao revelar dois sites com o mesmo dominio, o
            // CREATE INDEX falha e a migration aborta — preferimos falhar a
            // descartar dados silenciosamente.
            migrationBuilder.Sql(
                "UPDATE \"Sites\" SET \"CustomDomain\" = lower(\"CustomDomain\") WHERE \"CustomDomain\" IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_CustomDomain",
                table: "Sites",
                column: "CustomDomain",
                unique: true,
                filter: "\"CustomDomain\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sites_CustomDomain",
                table: "Sites");
        }
    }
}
