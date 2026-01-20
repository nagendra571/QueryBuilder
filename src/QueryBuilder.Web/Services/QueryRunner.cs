using System.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Models.Queries;

namespace QueryBuilder.Web.Services;

public class QueryRunner
{
    private const int MaxRows = 500;
    private readonly ApplicationDbContext _dbContext;
    private readonly IDataProtector _protector;

    public QueryRunner(ApplicationDbContext dbContext, IDataProtectionProvider dataProtectionProvider)
    {
        _dbContext = dbContext;
        _protector = dataProtectionProvider.CreateProtector("QueryBuilder.DataSources.ConnectionString");
    }

    public async Task<QueryResultViewModel> RunAsync(int dataSourceId, string sqlText)
    {
        var dataSource = await _dbContext.DataSources.AsNoTracking().FirstOrDefaultAsync(ds => ds.Id == dataSourceId);
        if (dataSource == null)
        {
            return new QueryResultViewModel { Success = false, ErrorMessage = "Data source not found." };
        }

        var connectionString = _protector.Unprotect(dataSource.ConnectionStringEncrypted);
        var stopwatch = Stopwatch.StartNew();
        var result = new QueryResultViewModel();

        try
        {
            var columns = new List<string>();
            var rows = new List<IReadOnlyList<string?>>();

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sqlText, connection)
            {
                CommandTimeout = 60
            };

            await using var reader = await command.ExecuteReaderAsync();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            var rowCount = 0;
            while (await reader.ReadAsync() && rowCount < MaxRows)
            {
                var row = new string?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i));
                }
                rows.Add(row);
                rowCount++;
            }

            stopwatch.Stop();
            result.Success = true;
            result.Columns = columns;
            result.Rows = rows;
            result.RowCount = rowCount;
            result.DurationMs = (int)stopwatch.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.DurationMs = (int)stopwatch.ElapsedMilliseconds;
        }

        return result;
    }
}
