using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePisarnica.Migrations
{
    /// <inheritdoc />
    public partial class AddAssigments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Naziv = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sifra = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Aktivan = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProtocolEntryId = table.Column<int>(type: "int", nullable: false),
                    DodijeljenOdjelId = table.Column<int>(type: "int", nullable: true),
                    DodijeljenUserId = table.Column<int>(type: "int", nullable: true),
                    Rok = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Prioritet = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DatumDodjele = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DatumZavrsetka = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Napomena = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProtocolEntryId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assignments_Departments_DodijeljenOdjelId",
                        column: x => x.DodijeljenOdjelId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assignments_ProtocolEntries_ProtocolEntryId",
                        column: x => x.ProtocolEntryId,
                        principalTable: "ProtocolEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Assignments_ProtocolEntries_ProtocolEntryId1",
                        column: x => x.ProtocolEntryId1,
                        principalTable: "ProtocolEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Assignments_Users_DodijeljenUserId",
                        column: x => x.DodijeljenUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_DepartmentId",
                table: "Users",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_DodijeljenOdjelId",
                table: "Assignments",
                column: "DodijeljenOdjelId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_DodijeljenUserId",
                table: "Assignments",
                column: "DodijeljenUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_ProtocolEntryId",
                table: "Assignments",
                column: "ProtocolEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_ProtocolEntryId1",
                table: "Assignments",
                column: "ProtocolEntryId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                table: "Users",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Assignments");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Users_DepartmentId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Users");
        }
    }
}
