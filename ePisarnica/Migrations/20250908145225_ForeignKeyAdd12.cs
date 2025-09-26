using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePisarnica.Migrations
{
    /// <inheritdoc />
    public partial class ForeignKeyAdd12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DigitalSignatures_Documents_DocumentId",
                table: "DigitalSignatures");

            migrationBuilder.AddForeignKey(
                name: "FK_DigitalSignatures_Documents_DocumentId",
                table: "DigitalSignatures",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DigitalSignatures_Documents_DocumentId",
                table: "DigitalSignatures");

            migrationBuilder.AddForeignKey(
                name: "FK_DigitalSignatures_Documents_DocumentId",
                table: "DigitalSignatures",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id");
        }
    }
}
