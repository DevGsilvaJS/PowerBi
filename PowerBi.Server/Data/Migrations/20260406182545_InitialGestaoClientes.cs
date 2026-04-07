using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PowerBi.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialGestaoClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gestaoclientes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    senha = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    chave_ws = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    identificador = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    lojas = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gestaoclientes", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gestaoclientes");
        }
    }
}
