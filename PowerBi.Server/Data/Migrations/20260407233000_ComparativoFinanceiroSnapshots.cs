using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PowerBi.Server.Data.Migrations;

/// <inheritdoc />
public partial class ComparativoFinanceiroSnapshots : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "comparativo_financeiro_ultima_consulta_utc",
                table: "gestaoclientes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "comparativo_financeiro_snapshot_pagar",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    gestao_cliente_id = table.Column<int>(type: "integer", nullable: false),
                    ano_menor = table.Column<int>(type: "integer", nullable: false),
                    ano_maior = table.Column<int>(type: "integer", nullable: false),
                    loja_param = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    serie_json = table.Column<string>(type: "text", nullable: false),
                    formas_json = table.Column<string>(type: "text", nullable: false),
                    atualizado_em_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comparativo_financeiro_snapshot_pagar", x => x.id);
                    table.ForeignKey(
                        name: "fk_cmp_fin_pagar_gestao_cliente",
                        column: x => x.gestao_cliente_id,
                        principalTable: "gestaoclientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comparativo_financeiro_snapshot_receber",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    gestao_cliente_id = table.Column<int>(type: "integer", nullable: false),
                    ano_menor = table.Column<int>(type: "integer", nullable: false),
                    ano_maior = table.Column<int>(type: "integer", nullable: false),
                    loja_param = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    serie_json = table.Column<string>(type: "text", nullable: false),
                    formas_json = table.Column<string>(type: "text", nullable: false),
                    atualizado_em_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comparativo_financeiro_snapshot_receber", x => x.id);
                    table.ForeignKey(
                        name: "fk_cmp_fin_receber_gestao_cliente",
                        column: x => x.gestao_cliente_id,
                        principalTable: "gestaoclientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_cmp_fin_pagar_cliente_anos_loja",
                table: "comparativo_financeiro_snapshot_pagar",
                columns: new[] { "gestao_cliente_id", "ano_menor", "ano_maior", "loja_param" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_cmp_fin_receber_cliente_anos_loja",
                table: "comparativo_financeiro_snapshot_receber",
                columns: new[] { "gestao_cliente_id", "ano_menor", "ano_maior", "loja_param" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comparativo_financeiro_snapshot_pagar");

            migrationBuilder.DropTable(
                name: "comparativo_financeiro_snapshot_receber");

            migrationBuilder.DropColumn(
                name: "comparativo_financeiro_ultima_consulta_utc",
                table: "gestaoclientes");
        }
}
