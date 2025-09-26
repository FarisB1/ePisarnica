using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePisarnica.Migrations
{
    /// <inheritdoc />
    public partial class AddDigitalSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProtocolEntries_DocumentId",
                table: "ProtocolEntries");

            migrationBuilder.AddColumn<int>(
                name: "DocumentId1",
                table: "ProtocolEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DigitalSignatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SignatureData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignatureHash = table.Column<string>(type: "nvarchar(44)", maxLength: 44, nullable: false),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsValid = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ValidatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigitalSignatures_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DigitalSignatures_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SignatureCertificates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PublicKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "DATEADD(year, 1, GETDATE())"),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RevocationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignatureCertificates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolEntries_DocumentId",
                table: "ProtocolEntries",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolEntries_DocumentId1",
                table: "ProtocolEntries",
                column: "DocumentId1",
                unique: true,
                filter: "[DocumentId1] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalSignatures_DocumentId",
                table: "DigitalSignatures",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalSignatures_IsValid",
                table: "DigitalSignatures",
                column: "IsValid");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalSignatures_SignedAt",
                table: "DigitalSignatures",
                column: "SignedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalSignatures_UserId",
                table: "DigitalSignatures",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureCertificates_ExpiresAt",
                table: "SignatureCertificates",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureCertificates_IsRevoked",
                table: "SignatureCertificates",
                column: "IsRevoked");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureCertificates_UserId",
                table: "SignatureCertificates",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProtocolEntries_Documents_DocumentId1",
                table: "ProtocolEntries",
                column: "DocumentId1",
                principalTable: "Documents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProtocolEntries_Documents_DocumentId1",
                table: "ProtocolEntries");

            migrationBuilder.DropTable(
                name: "DigitalSignatures");

            migrationBuilder.DropTable(
                name: "SignatureCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProtocolEntries_DocumentId",
                table: "ProtocolEntries");

            migrationBuilder.DropIndex(
                name: "IX_ProtocolEntries_DocumentId1",
                table: "ProtocolEntries");

            migrationBuilder.DropColumn(
                name: "DocumentId1",
                table: "ProtocolEntries");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolEntries_DocumentId",
                table: "ProtocolEntries",
                column: "DocumentId",
                unique: true,
                filter: "[DocumentId] IS NOT NULL");
        }
    }
}
