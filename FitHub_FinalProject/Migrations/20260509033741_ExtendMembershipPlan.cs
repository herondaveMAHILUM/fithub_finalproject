using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitHub_FinalProject.Migrations
{
    /// <inheritdoc />
    public partial class ExtendMembershipPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Features",
                table: "MembershipPlans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxMembers",
                table: "MembershipPlans",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MembershipPlans",
                keyColumn: "PlanId",
                keyValue: 1,
                columns: new[] { "Features", "MaxMembers" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MembershipPlans",
                keyColumn: "PlanId",
                keyValue: 2,
                columns: new[] { "Features", "MaxMembers" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MembershipPlans",
                keyColumn: "PlanId",
                keyValue: 3,
                columns: new[] { "Features", "MaxMembers" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Features",
                table: "MembershipPlans");

            migrationBuilder.DropColumn(
                name: "MaxMembers",
                table: "MembershipPlans");
        }
    }
}
