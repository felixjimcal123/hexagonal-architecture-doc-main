using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.ApplicationCore.Exceptions;
using GtMotive.Estimate.Microservice.Domain.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GtMotive.Estimate.Microservice.Infrastructure.Persistence
{
    public sealed class RentingUnitOfWork(RentingDbContext context) : IUnitOfWork
    {
        public async Task<int> Save()
        {
            try
            {
                return await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                // Two requests tried to return the same rental from stale snapshots.
                throw new ConflictException("The rental was modified by another request.", exception);
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
            {
                // SQLITE_CONSTRAINT_UNIQUE: SQLite enforces the invariant under concurrent requests.
                throw new ConflictException("A unique constraint was violated.", exception);
            }
        }
    }
}
