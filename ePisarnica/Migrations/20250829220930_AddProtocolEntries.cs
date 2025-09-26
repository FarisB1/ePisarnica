using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePisarnica.Migrations
{
    /// <inheritdoc />
    public partial class AddProtocolEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProtocolEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BrojProtokola = table.Column<int>(type: "int", nullable: false),
                    Datum = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Stranka = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Napomena = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentId = table.Column<int>(type: "int", nullable: true),
                    QrCodePath = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtocolEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProtocolEntries_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolEntries_DocumentId",
                table: "ProtocolEntries",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProtocolEntries");
        }
    }
}
