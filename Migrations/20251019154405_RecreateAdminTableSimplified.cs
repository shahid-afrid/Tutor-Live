using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorLiveMentor10.Migrations
{
    /// <inheritdoc />
    public partial class RecreateAdminTableSimplified : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "LastLoginDate",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Admins");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "Admins",
                newName: "Department");

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "AdminId",
                keyValue: 1,
                columns: new[] { "Department", "Email", "Password" },
                values: new object[] { "CSE", "cseds@rgmcet.edu.in", "admin123" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Department",
                table: "Admins",
                newName: "Role");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Admins",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Admins",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginDate",
                table: "Admins",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Admins",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "AdminId",
                keyValue: 1,
                columns: new[] { "CreatedDate", "Email", "IsActive", "LastLoginDate", "Name", "Password", "Role" },
                values: new object[] { new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@rgmcet.edu.in", true, null, "System Administrator", "Admin@123", "Super Administrator" });
        }
    }
}
