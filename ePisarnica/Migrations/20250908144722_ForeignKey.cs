using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePisarnica.Migrations
{
    /// <inheritdoc />
    public partial class ForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProtocolEntries_Documents_DocumentId",
                table: "ProtocolEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_ProtocolEntries_Documents_DocumentId",
                table: "ProtocolEntries",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProtocolEntries_Documents_DocumentId",
                table: "ProtocolEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_ProtocolEntries_Documents_DocumentId",
                table: "ProtocolEntries",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id");
        }
    }
}
