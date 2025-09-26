using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePisarnica.Migrations
{
    /// <inheritdoc />
    public partial class AddProtocolEntryAdditionalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Adresa",
                table: "ProtocolEntries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Dostavio",
                table: "ProtocolEntries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "ProtocolEntries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Hitno",
                table: "ProtocolEntries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Primalac",
                table: "ProtocolEntries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RokZaOdgovor",
                table: "ProtocolEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefon",
                table: "ProtocolEntries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VrstaPostupka",
                table: "ProtocolEntries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Adresa",
                table: "ProtocolEntries");

            migrationBuilder.DropColumn(
                name: "Dostavio",
                table: "ProtocolEntries");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "ProtocolEntries");

            migrationBuilder.DropColumn(
                name: "Hitno",
                table: "ProtocolEntries");

            migrationBuilder.DropColumn(
                name: "Primalac",
                table: "ProtocolEntries");

            migrationBuilder.DropColumn(
                name: "RokZaOdgovor",
                table: "ProtocolEntries");

            migrationBuilder.DropColumn(
                name: "Telefon",
                table: "ProtocolEntries");

            migrationBuilder.DropColumn(
                name: "VrstaPostupka",
                table: "ProtocolEntries");
        }
    }
}
