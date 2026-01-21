using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QueryBuilder.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVisualizationAutoRefresh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAutoRefreshEnabled",
                table: "Visualizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AutoRefreshIntervalSeconds",
                table: "Visualizations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAutoRefreshEnabled",
                table: "Visualizations");

            migrationBuilder.DropColumn(
                name: "AutoRefreshIntervalSeconds",
                table: "Visualizations");
        }
    }
}
