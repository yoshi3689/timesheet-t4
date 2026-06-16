using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TimesheetApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveParentWorkPackageProjectId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TiDB rejects DROP COLUMN when the column is covered by an index in the same
            // ALTER TABLE (planning-time check). Drop FK, then the FK-named index, then the
            // separately-named IX index — all in separate statements — before dropping the column.
            migrationBuilder.Sql(@"
                ALTER TABLE `WorkPackages`
                  DROP FOREIGN KEY `FK_WorkPackages_WorkPackages_ParentWorkPackageId_ParentWorkPack~`,
                  DROP INDEX `FK_WorkPackages_WorkPackages_ParentWorkPackageId_ParentWorkPack~`;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE `WorkPackages`
                  DROP INDEX `IX_WorkPackages_ParentWorkPackageId_ParentWorkPackageProjectId`;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE `WorkPackages`
                  DROP COLUMN `ParentWorkPackageProjectId`;
            ");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "37e7120e-5609-4a75-bec4-908ecb7497c2");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8ba1a177-5c9e-4b6c-87a7-44c8f44d0e5f");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "ef38ee2e-5e03-462f-9254-2cc57c51eebf");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "05a39b56-553e-43cd-9981-5857ed127f57", null, "Admin", "ADMIN" },
                    { "14afb731-30ef-485a-aedf-a1564905a6ce", null, "HR", "HR" },
                    { "262a897f-56f7-495a-b883-fdde84455667", null, "Supervisor", "SUPERVISOR" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkPackages_ParentWorkPackageId_ProjectId",
                table: "WorkPackages",
                columns: new[] { "ParentWorkPackageId", "ProjectId" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkPackages_WorkPackages_ParentWorkPackageId_ProjectId",
                table: "WorkPackages",
                columns: new[] { "ParentWorkPackageId", "ProjectId" },
                principalTable: "WorkPackages",
                principalColumns: new[] { "WorkPackageId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkPackages_WorkPackages_ParentWorkPackageId_ProjectId",
                table: "WorkPackages");

            migrationBuilder.DropIndex(
                name: "IX_WorkPackages_ParentWorkPackageId_ProjectId",
                table: "WorkPackages");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "05a39b56-553e-43cd-9981-5857ed127f57");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "14afb731-30ef-485a-aedf-a1564905a6ce");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "262a897f-56f7-495a-b883-fdde84455667");

            migrationBuilder.AddColumn<int>(
                name: "ParentWorkPackageProjectId",
                table: "WorkPackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "37e7120e-5609-4a75-bec4-908ecb7497c2", null, "HR", "HR" },
                    { "8ba1a177-5c9e-4b6c-87a7-44c8f44d0e5f", null, "Admin", "ADMIN" },
                    { "ef38ee2e-5e03-462f-9254-2cc57c51eebf", null, "Supervisor", "SUPERVISOR" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkPackages_ParentWorkPackageId_ParentWorkPackageProjectId",
                table: "WorkPackages",
                columns: new[] { "ParentWorkPackageId", "ParentWorkPackageProjectId" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkPackages_WorkPackages_ParentWorkPackageId_ParentWorkPack~",
                table: "WorkPackages",
                columns: new[] { "ParentWorkPackageId", "ParentWorkPackageProjectId" },
                principalTable: "WorkPackages",
                principalColumns: new[] { "WorkPackageId", "ProjectId" });
        }
    }
}
