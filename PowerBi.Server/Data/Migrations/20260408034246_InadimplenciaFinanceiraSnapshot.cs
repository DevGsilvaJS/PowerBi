using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PowerBi.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class InadimplenciaFinanceiraSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_comparativo_financeiro_snapshot_pagar_gestaoclientes_gestao~",
                table: "comparativo_financeiro_snapshot_pagar");

            migrationBuilder.DropForeignKey(
                name: "FK_comparativo_financeiro_snapshot_receber_gestaoclientes_gest~",
                table: "comparativo_financeiro_snapshot_receber");

            migrationBuilder.AddForeignKey(
                name: "fk_comparativo_financeiro_snapshot_pagar_gestaoclientes_gestao",
                table: "comparativo_financeiro_snapshot_pagar",
                column: "gestao_cliente_id",
                principalTable: "gestaoclientes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_comparativo_financeiro_snapshot_receber_gestaoclientes_gest",
                table: "comparativo_financeiro_snapshot_receber",
                column: "gestao_cliente_id",
                principalTable: "gestaoclientes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_comparativo_financeiro_snapshot_pagar_gestaoclientes_gestao",
                table: "comparativo_financeiro_snapshot_pagar");

            migrationBuilder.DropForeignKey(
                name: "fk_comparativo_financeiro_snapshot_receber_gestaoclientes_gest",
                table: "comparativo_financeiro_snapshot_receber");

            migrationBuilder.AddForeignKey(
                name: "FK_comparativo_financeiro_snapshot_pagar_gestaoclientes_gestao~",
                table: "comparativo_financeiro_snapshot_pagar",
                column: "gestao_cliente_id",
                principalTable: "gestaoclientes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_comparativo_financeiro_snapshot_receber_gestaoclientes_gest~",
                table: "comparativo_financeiro_snapshot_receber",
                column: "gestao_cliente_id",
                principalTable: "gestaoclientes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
