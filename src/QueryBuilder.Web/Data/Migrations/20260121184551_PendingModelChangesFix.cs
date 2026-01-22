using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QueryBuilder.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelChangesFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_DashboardParameterMappings_DashboardWidgetId'
      AND object_id = OBJECT_ID('[DashboardParameterMappings]'))
BEGIN
    DROP INDEX [IX_DashboardParameterMappings_DashboardWidgetId] ON [DashboardParameterMappings];
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[QueryParameterDefinitions]', N'U') IS NOT NULL
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[QueryParameterDefinitions]') AND [c].[name] = N'SettingsJson');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [QueryParameterDefinitions] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [QueryParameterDefinitions] ADD DEFAULT N'{}' FOR [SettingsJson];
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[DashboardParameterControls]', N'U') IS NOT NULL
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DashboardParameterControls]') AND [c].[name] = N'SettingsJson');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [DashboardParameterControls] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [DashboardParameterControls] ADD DEFAULT N'{}' FOR [SettingsJson];
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SettingsJson",
                table: "QueryParameterDefinitions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "{}");

            migrationBuilder.AlterColumn<string>(
                name: "SettingsJson",
                table: "DashboardParameterControls",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "{}");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardParameterMappings_DashboardWidgetId",
                table: "DashboardParameterMappings",
                column: "DashboardWidgetId");
        }
    }
}
