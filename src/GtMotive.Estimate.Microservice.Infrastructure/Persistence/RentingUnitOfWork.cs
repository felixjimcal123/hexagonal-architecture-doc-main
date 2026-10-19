using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.Domain.Interfaces;

namespace GtMotive.Estimate.Microservice.Infrastructure.Persistence
{
    public sealed class RentingUnitOfWork(RentingDbContext context) : IUnitOfWork
    {
        public Task<int> Save()
        {
            return context.SaveChangesAsync();
        }
    }
}
