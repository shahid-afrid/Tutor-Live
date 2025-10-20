using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorLiveMentor10.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDepartmentToCSEDS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "AdminId",
                keyValue: 1,
                column: "Department",
                value: "CSEDS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "AdminId",
                keyValue: 1,
                column: "Department",
                value: "CSE");
        }
    }
}
