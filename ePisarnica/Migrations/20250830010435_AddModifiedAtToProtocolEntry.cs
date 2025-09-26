using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePisarnica.Migrations
{
    /// <inheritdoc />
    public partial class AddModifiedAtToProtocolEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "ProtocolEntries",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "ProtocolEntries");
        }
    }
}
