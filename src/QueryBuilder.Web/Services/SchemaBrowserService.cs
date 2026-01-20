using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Models.Queries;

namespace QueryBuilder.Web.Services;

public class SchemaBrowserService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDataProtector _protector;

    public SchemaBrowserService(ApplicationDbContext dbContext, IDataProtectionProvider dataProtectionProvider)
    {
        _dbContext = dbContext;
        _protector = dataProtectionProvider.CreateProtector("QueryBuilder.DataSources.ConnectionString");
    }

    public async Task<SchemaResultViewModel> GetTablesAsync(int dataSourceId)
    {
        var dataSource = await _dbContext.DataSources.AsNoTracking().FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.IsActive);
        if (dataSource == null)
        {
            return new SchemaResultViewModel { Success = false, ErrorMessage = "Data source not found." };
        }

        if (dataSource.Type != DataSourceType.SqlServer)
        {
            return new SchemaResultViewModel { Success = false, ErrorMessage = "Schema browsing not supported for this data source." };
        }

        try
        {
            var tables = new List<SchemaTableInfo>();
            var connectionString = _protector.Unprotect(dataSource.ConnectionStringEncrypted);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = """
                SELECT s.name AS SchemaName, t.name AS TableName
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                ORDER BY s.name, t.name
                """;

            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(new SchemaTableInfo
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1)
                });
            }

            return new SchemaResultViewModel { Success = true, Tables = tables };
        }
        catch (Exception ex)
        {
            return new SchemaResultViewModel { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SchemaColumnsResultViewModel> GetColumnsAsync(int dataSourceId, string schemaName, string tableName)
    {
        var dataSource = await _dbContext.DataSources.AsNoTracking().FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.IsActive);
        if (dataSource == null)
        {
            return new SchemaColumnsResultViewModel { Success = false, ErrorMessage = "Data source not found." };
        }

        if (dataSource.Type != DataSourceType.SqlServer)
        {
            return new SchemaColumnsResultViewModel { Success = false, ErrorMessage = "Schema browsing not supported for this data source." };
        }

        try
        {
            var columns = new List<SchemaColumnInfo>();
            var connectionString = _protector.Unprotect(dataSource.ConnectionStringEncrypted);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = """
                SELECT c.name, t.name
                FROM sys.columns c
                INNER JOIN sys.tables tb ON c.object_id = tb.object_id
                INNER JOIN sys.schemas s ON tb.schema_id = s.schema_id
                INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE s.name = @schema AND tb.name = @table
                ORDER BY c.column_id
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@schema", schemaName);
            command.Parameters.AddWithValue("@table", tableName);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(new SchemaColumnInfo
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1)
                });
            }

            return new SchemaColumnsResultViewModel { Success = true, Columns = columns };
        }
        catch (Exception ex)
        {
            return new SchemaColumnsResultViewModel { Success = false, ErrorMessage = ex.Message };
        }
    }
}
