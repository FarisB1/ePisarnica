using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePisarnica.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProtocolEntries_DocumentId",
                table: "ProtocolEntries");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Zaprimljeno",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolEntries_DocumentId",
                table: "ProtocolEntries",
                column: "DocumentId",
                unique: true,
                filter: "[DocumentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProtocolEntries_DocumentId",
                table: "ProtocolEntries");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "Zaprimljeno");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolEntries_DocumentId",
                table: "ProtocolEntries",
                column: "DocumentId");
        }
    }
}
