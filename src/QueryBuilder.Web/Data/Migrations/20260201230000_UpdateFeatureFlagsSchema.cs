using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QueryBuilder.Web.Data;

#nullable disable

namespace QueryBuilder.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFeatureFlagsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeatureFlags_Key",
                table: "FeatureFlags");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "FeatureFlags",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "FeatureFlags",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "FeatureFlags",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "FeatureFlags",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "FeatureFlags",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_Key",
                table: "FeatureFlags",
                column: "Key",
                unique: true);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM FeatureFlags WHERE [Key] = 'NewAppearance')
                BEGIN
                    UPDATE FeatureFlags
                    SET [Description] = ISNULL([Description], 'Enable compact enterprise UI.')
                    WHERE [Key] = 'NewAppearance'
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeatureFlags_Key",
                table: "FeatureFlags");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "FeatureFlags",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "FeatureFlags",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.DropColumn(
                name: "Description",
                table: "FeatureFlags");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "FeatureFlags");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "FeatureFlags");
        }
    }
}
