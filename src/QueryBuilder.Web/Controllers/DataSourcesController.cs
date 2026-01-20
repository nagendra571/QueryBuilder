using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Models.DataSources;

namespace QueryBuilder.Web.Controllers;

[Authorize(Roles = "Admin")]
public class DataSourcesController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDataProtector _protector;

    public DataSourcesController(ApplicationDbContext dbContext, IDataProtectionProvider dataProtectionProvider)
    {
        _dbContext = dbContext;
        _protector = dataProtectionProvider.CreateProtector("QueryBuilder.DataSources.ConnectionString");
    }

    public async Task<IActionResult> Index()
    {
        var dataSources = await _dbContext.DataSources
            .OrderBy(ds => ds.Name)
            .ToListAsync();

        return View(dataSources);
    }

    public IActionResult Create()
    {
        return View(new DataSourceEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DataSourceEditViewModel model)
    {
        if (string.Equals(model.SubmitAction, "test", StringComparison.OrdinalIgnoreCase))
        {
            await TestConnectionAsync(model.ConnectionString);
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.ConnectionString))
        {
            ModelState.AddModelError(nameof(model.ConnectionString), "Connection string is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var now = DateTimeOffset.UtcNow;
        var dataSource = new DataSource
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            Type = model.Type,
            ConnectionStringEncrypted = _protector.Protect(model.ConnectionString!),
            IsActive = model.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.DataSources.Add(dataSource);
        await _dbContext.SaveChangesAsync();

        TempData["StatusMessage"] = "Data source created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var dataSource = await _dbContext.DataSources.FirstOrDefaultAsync(ds => ds.Id == id);
        if (dataSource == null)
        {
            return NotFound();
        }

        var model = new DataSourceEditViewModel
        {
            Id = dataSource.Id,
            Name = dataSource.Name,
            Description = dataSource.Description,
            Type = dataSource.Type,
            IsActive = dataSource.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DataSourceEditViewModel model)
    {
        var dataSource = await _dbContext.DataSources.FirstOrDefaultAsync(ds => ds.Id == id);
        if (dataSource == null)
        {
            return NotFound();
        }

        if (string.Equals(model.SubmitAction, "test", StringComparison.OrdinalIgnoreCase))
        {
            var connectionToTest = string.IsNullOrWhiteSpace(model.ConnectionString)
                ? _protector.Unprotect(dataSource.ConnectionStringEncrypted)
                : model.ConnectionString;
            await TestConnectionAsync(connectionToTest);
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        dataSource.Name = model.Name.Trim();
        dataSource.Description = model.Description?.Trim();
        dataSource.Type = model.Type;
        dataSource.IsActive = model.IsActive;
        dataSource.UpdatedAt = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(model.ConnectionString))
        {
            dataSource.ConnectionStringEncrypted = _protector.Protect(model.ConnectionString);
        }

        await _dbContext.SaveChangesAsync();

        TempData["StatusMessage"] = "Data source updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var dataSource = await _dbContext.DataSources.FirstOrDefaultAsync(ds => ds.Id == id);
        if (dataSource == null)
        {
            return NotFound();
        }

        return View(dataSource);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var dataSource = await _dbContext.DataSources.FirstOrDefaultAsync(ds => ds.Id == id);
        if (dataSource == null)
        {
            return NotFound();
        }

        _dbContext.DataSources.Remove(dataSource);
        await _dbContext.SaveChangesAsync();

        TempData["StatusMessage"] = "Data source deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task TestConnectionAsync(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            ViewData["TestResult"] = "Connection string is required to test connectivity.";
            ViewData["TestResultStatus"] = "danger";
            return;
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            ViewData["TestResult"] = "Connection successful.";
            ViewData["TestResultStatus"] = "success";
        }
        catch (Exception ex)
        {
            ViewData["TestResult"] = $"Connection failed: {ex.Message}";
            ViewData["TestResultStatus"] = "danger";
        }
    }
}
