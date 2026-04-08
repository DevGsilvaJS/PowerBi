using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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

            migrationBuilder.CreateTable(
                name: "inadimplencia_financeira_snapshot",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    gestao_cliente_id = table.Column<int>(type: "integer", nullable: false),
                    loja_param = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    formas_json = table.Column<string>(type: "text", nullable: false),
                    periodo_emissao_inicio = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    periodo_emissao_fim = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    atualizado_em_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inadimplencia_financeira_snapshot", x => x.id);
                    table.ForeignKey(
                        name: "fk_inadimplencia_financeira_snapshot_gestaoclientes_gestao_cli",
                        column: x => x.gestao_cliente_id,
                        principalTable: "gestaoclientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_inadimpl_fin_cliente_loja",
                table: "inadimplencia_financeira_snapshot",
                columns: new[] { "gestao_cliente_id", "loja_param" },
                unique: true);

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

            migrationBuilder.DropTable(
                name: "inadimplencia_financeira_snapshot");

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
