#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.Domain.Entities;
using GtMotive.Estimate.Microservice.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GtMotive.Estimate.Microservice.Infrastructure.Persistence
{
    public sealed class RentalRepository(RentingDbContext context) : IRentalRepository
    {
        public Task<Rental?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return context.Rentals.SingleOrDefaultAsync(rental => rental.Id == id, cancellationToken);
        }

        public Task<bool> HasActiveRentalForCustomerAsync(string customerId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(customerId);

            return context.Rentals.AnyAsync(
                rental => rental.CustomerId == customerId && rental.ReturnedAtUtc == null,
                cancellationToken);
        }

        public Task<bool> HasActiveRentalForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
        {
            return context.Rentals.AnyAsync(
                rental => rental.VehicleId == vehicleId && rental.ReturnedAtUtc == null,
                cancellationToken);
        }

        public async Task AddAsync(Rental rental, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rental);

            await context.Rentals.AddAsync(rental, cancellationToken);
        }
    }
}
