using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_StudentId",
                table: "Enrollments");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_Student_Course_Semester",
                table: "Enrollments",
                columns: new[] { "StudentId", "CourseId", "SemesterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_Student_Course_Semester",
                table: "Enrollments");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId",
                table: "Enrollments",
                column: "StudentId");
        }
    }
}
