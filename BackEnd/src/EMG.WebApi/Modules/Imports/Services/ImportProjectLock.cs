using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Imports.Services;

internal static class ImportProjectLock
{
    // A full row lock serializes import decisions and waits for new ledger rows referencing the project.
    // SQLite already serializes writers in the enclosing transaction.
    public static Task AcquireAsync(EmgDbContext db, int projectId, CancellationToken cancellationToken) =>
        db.Database.IsNpgsql()
            ? db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM projects WHERE \"Id\" = {projectId} FOR UPDATE", cancellationToken)
            : Task.CompletedTask;
}
