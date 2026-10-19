#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.Domain.Entities;
using GtMotive.Estimate.Microservice.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GtMotive.Estimate.Microservice.Infrastructure.Persistence
{
    public sealed class VehicleRepository(RentingDbContext context) : IVehicleRepository
    {
        public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return context.Vehicles.SingleOrDefaultAsync(vehicle => vehicle.Id == id, cancellationToken);
        }

        public Task<bool> ExistsByLicensePlateAsync(string licensePlate, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(licensePlate);

            return context.Vehicles.AnyAsync(vehicle => vehicle.LicensePlate == licensePlate, cancellationToken);
        }

        public async Task<IReadOnlyList<Vehicle>> ListAvailableAsync(DateOnly asOf, CancellationToken cancellationToken = default)
        {
            var earliestCandidate = asOf.AddYears(-5);
            var candidates = await context.Vehicles
                .AsNoTracking()
                .Where(vehicle => vehicle.ManufacturedOn >= earliestCandidate && vehicle.ManufacturedOn <= asOf)
                .Where(vehicle => !context.Rentals.Any(rental => rental.VehicleId == vehicle.Id && rental.ReturnedAtUtc == null))
                .OrderBy(vehicle => vehicle.LicensePlate)
                .ToListAsync(cancellationToken);

            // AddYears(-5) is a coarse SQL filter. Check the exact domain rule for leap-day boundaries.
            return [.. candidates.Where(vehicle => vehicle.IsEligibleForRental(asOf))];
        }

        public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(vehicle);

            await context.Vehicles.AddAsync(vehicle, cancellationToken);
        }
    }
}
