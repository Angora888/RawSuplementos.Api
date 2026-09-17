using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RawSuplementos.Api.Migrations
{
    public partial class AgregarMultitenancy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Negocios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    WhatsApp = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Direccion = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ColorPrimario = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ColorSecundario = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Negocios", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_Negocios_Slug", table: "Negocios", column: "Slug", unique: true);

            // Se agregan primero como nullable para poder conservar y asociar los datos existentes.
            migrationBuilder.AddColumn<int>(name: "NegocioId", table: "Usuarios", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "NegocioId", table: "Categorias", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "NegocioId", table: "Clientes", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "NegocioId", table: "Productos", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "NegocioId", table: "Ventas", type: "integer", nullable: true);

            // Tenant inicial para todos los datos RAW que ya existen.
            migrationBuilder.Sql("""
                INSERT INTO "Negocios"
                    ("Nombre", "Slug", "WhatsApp", "Activo", "FechaCreacion")
                VALUES
                    ('RAW Suplementos', 'raw-suplementos', '50672509174', TRUE, NOW());
                """);

            // No se asume que el Id sea 1: se obtiene por el slug único.
            migrationBuilder.Sql("""
                UPDATE "Usuarios"
                SET "NegocioId" = (SELECT "Id" FROM "Negocios" WHERE "Slug" = 'raw-suplementos')
                WHERE "NegocioId" IS NULL;

                UPDATE "Categorias"
                SET "NegocioId" = (SELECT "Id" FROM "Negocios" WHERE "Slug" = 'raw-suplementos')
                WHERE "NegocioId" IS NULL;

                UPDATE "Clientes"
                SET "NegocioId" = (SELECT "Id" FROM "Negocios" WHERE "Slug" = 'raw-suplementos')
                WHERE "NegocioId" IS NULL;

                UPDATE "Productos"
                SET "NegocioId" = (SELECT "Id" FROM "Negocios" WHERE "Slug" = 'raw-suplementos')
                WHERE "NegocioId" IS NULL;

                UPDATE "Ventas"
                SET "NegocioId" = (SELECT "Id" FROM "Negocios" WHERE "Slug" = 'raw-suplementos')
                WHERE "NegocioId" IS NULL;
                """);

            // Después del backfill los campos pasan a ser obligatorios.
            migrationBuilder.AlterColumn<int>(name: "NegocioId", table: "Usuarios", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "NegocioId", table: "Categorias", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "NegocioId", table: "Clientes", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "NegocioId", table: "Productos", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "NegocioId", table: "Ventas", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);

            // Sustituimos índices globales por índices tenant-aware donde corresponde.
            migrationBuilder.DropIndex(name: "IX_Categorias_Nombre", table: "Categorias");
            migrationBuilder.DropIndex(name: "IX_Clientes_Telefono", table: "Clientes");

            migrationBuilder.CreateIndex(name: "IX_Usuarios_NegocioId", table: "Usuarios", column: "NegocioId");
            migrationBuilder.CreateIndex(name: "IX_Categorias_NegocioId_Nombre", table: "Categorias", columns: new[] { "NegocioId", "Nombre" });
            migrationBuilder.CreateIndex(name: "IX_Clientes_NegocioId_Telefono", table: "Clientes", columns: new[] { "NegocioId", "Telefono" });
            migrationBuilder.CreateIndex(name: "IX_Productos_NegocioId", table: "Productos", column: "NegocioId");
            migrationBuilder.CreateIndex(name: "IX_Ventas_NegocioId", table: "Ventas", column: "NegocioId");

            migrationBuilder.AddForeignKey(name: "FK_Usuarios_Negocios_NegocioId", table: "Usuarios", column: "NegocioId", principalTable: "Negocios", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Categorias_Negocios_NegocioId", table: "Categorias", column: "NegocioId", principalTable: "Negocios", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Clientes_Negocios_NegocioId", table: "Clientes", column: "NegocioId", principalTable: "Negocios", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Productos_Negocios_NegocioId", table: "Productos", column: "NegocioId", principalTable: "Negocios", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Ventas_Negocios_NegocioId", table: "Ventas", column: "NegocioId", principalTable: "Negocios", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Usuarios_Negocios_NegocioId", table: "Usuarios");
            migrationBuilder.DropForeignKey(name: "FK_Categorias_Negocios_NegocioId", table: "Categorias");
            migrationBuilder.DropForeignKey(name: "FK_Clientes_Negocios_NegocioId", table: "Clientes");
            migrationBuilder.DropForeignKey(name: "FK_Productos_Negocios_NegocioId", table: "Productos");
            migrationBuilder.DropForeignKey(name: "FK_Ventas_Negocios_NegocioId", table: "Ventas");

            migrationBuilder.DropIndex(name: "IX_Usuarios_NegocioId", table: "Usuarios");
            migrationBuilder.DropIndex(name: "IX_Categorias_NegocioId_Nombre", table: "Categorias");
            migrationBuilder.DropIndex(name: "IX_Clientes_NegocioId_Telefono", table: "Clientes");
            migrationBuilder.DropIndex(name: "IX_Productos_NegocioId", table: "Productos");
            migrationBuilder.DropIndex(name: "IX_Ventas_NegocioId", table: "Ventas");

            migrationBuilder.DropColumn(name: "NegocioId", table: "Usuarios");
            migrationBuilder.DropColumn(name: "NegocioId", table: "Categorias");
            migrationBuilder.DropColumn(name: "NegocioId", table: "Clientes");
            migrationBuilder.DropColumn(name: "NegocioId", table: "Productos");
            migrationBuilder.DropColumn(name: "NegocioId", table: "Ventas");

            migrationBuilder.DropTable(name: "Negocios");

            migrationBuilder.CreateIndex(name: "IX_Categorias_Nombre", table: "Categorias", column: "Nombre");
            migrationBuilder.CreateIndex(name: "IX_Clientes_Telefono", table: "Clientes", column: "Telefono");
        }
    }
}
