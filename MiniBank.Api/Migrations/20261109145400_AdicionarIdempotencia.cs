using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniBank.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarIdempotencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdemPotencias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Chave = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    OperacaoFinanceiraId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdemPotencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdemPotencias_OperacoesFinanceiras_OperacaoFinanceiraId",
                        column: x => x.OperacaoFinanceiraId,
                        principalTable: "OperacoesFinanceiras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdemPotencias_Chave",
                table: "IdemPotencias",
                column: "Chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdemPotencias_OperacaoFinanceiraId",
                table: "IdemPotencias",
                column: "OperacaoFinanceiraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdemPotencias");
        }
    }
}
