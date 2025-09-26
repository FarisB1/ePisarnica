using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePisarnica.Migrations
{
    /// <inheritdoc />
    public partial class AddProtocolSignatureFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNew",
                table: "ProtocolEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSigned",
                table: "ProtocolEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SignatureNotes",
                table: "ProtocolEntries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SignedByUserId",
                table: "ProtocolEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedDate",
                table: "ProtocolEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolEntries_SignedByUserId",
                table: "ProtocolEntries",
                column: "SignedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProtocolEntries_Users_SignedByUserId",
                table: "ProtocolEntries",
                column: "SignedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProtocolEntries_Users_SignedByUserId",
                table: "ProtocolEntries");

            migrationBuilder.DropIndex(
                name: "IX_ProtocolEntries_SignedByUserId",
                table: "ProtocolEntries");

            migrationBuilder.DropColumn(
                name: "IsNew",
                table: "ProtocolEntries");

            migrationBuilder.DropColumn(
                name: "IsSigned",
                table: "ProtocolEntries");

            migrationBuilder.DropColumn(
                name: "SignatureNotes",
                table: "ProtocolEntries");

            migrationBuilder.DropColumn(
                name: "SignedByUserId",
                table: "ProtocolEntries");

            migrationBuilder.DropColumn(
                name: "SignedDate",
                table: "ProtocolEntries");
        }
    }
}
