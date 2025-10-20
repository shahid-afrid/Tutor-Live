using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorLiveMentor10.Migrations
{
    /// <inheritdoc />
    public partial class RecreateStudentWithStringId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Warning: This migration will result in data loss
            // Backup any important student data before running this migration
            
            // Step 1: Drop all foreign key constraints that reference Students table
            migrationBuilder.DropForeignKey(
                name: "FK_StudentEnrollments_Students_StudentId",
                table: "StudentEnrollments");

            // Step 2: Drop the StudentEnrollments table (will be recreated)
            migrationBuilder.DropTable(
                name: "StudentEnrollments");

            // Step 3: Drop the Students table (will be recreated)
            migrationBuilder.DropTable(
                name: "Students");

            // Step 4: Recreate Students table with string Id
            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegdNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Year = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectedSubject = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                });

            // Step 5: Recreate StudentEnrollments table with string StudentId
            migrationBuilder.CreateTable(
                name: "StudentEnrollments",
                columns: table => new
                {
                    StudentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AssignedSubjectId = table.Column<int>(type: "int", nullable: false),
                    StudentEnrollmentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentEnrollments", x => new { x.StudentId, x.AssignedSubjectId });
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_AssignedSubjects_AssignedSubjectId",
                        column: x => x.AssignedSubjectId,
                        principalTable: "AssignedSubjects",
                        principalColumn: "AssignedSubjectId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Step 6: Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_AssignedSubjectId",
                table: "StudentEnrollments",
                column: "AssignedSubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Warning: This down migration will also result in data loss
            // Recreate the original structure with int identity Id
            
            migrationBuilder.DropForeignKey(
                name: "FK_StudentEnrollments_Students_StudentId",
                table: "StudentEnrollments");

            migrationBuilder.DropTable(
                name: "StudentEnrollments");

            migrationBuilder.DropTable(
                name: "Students");

            // Recreate Students table with int identity Id
            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegdNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Year = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectedSubject = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                });

            // Recreate StudentEnrollments table with int StudentId
            migrationBuilder.CreateTable(
                name: "StudentEnrollments",
                columns: table => new
                {
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AssignedSubjectId = table.Column<int>(type: "int", nullable: false),
                    StudentEnrollmentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentEnrollments", x => new { x.StudentId, x.AssignedSubjectId });
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_AssignedSubjects_AssignedSubjectId",
                        column: x => x.AssignedSubjectId,
                        principalTable: "AssignedSubjects",
                        principalColumn: "AssignedSubjectId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_AssignedSubjectId",
                table: "StudentEnrollments",
                column: "AssignedSubjectId");
        }
    }
}
