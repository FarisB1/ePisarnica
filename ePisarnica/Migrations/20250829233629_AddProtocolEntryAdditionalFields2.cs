using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePisarnica.Migrations
{
    /// <inheritdoc />
    public partial class AddProtocolEntryAdditionalFields2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "ProtocolEntries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "ProtocolEntries");
        }
    }
}
