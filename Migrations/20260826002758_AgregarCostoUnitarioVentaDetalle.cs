using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RawSuplementos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCostoUnitarioVentaDetalle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostoUnitario",
                table: "VentaDetalles",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostoUnitario",
                table: "VentaDetalles");
        }
    }
}
