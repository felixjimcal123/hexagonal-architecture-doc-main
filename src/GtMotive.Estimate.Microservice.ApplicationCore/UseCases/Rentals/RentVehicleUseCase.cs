using System;
using System.Threading;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.ApplicationCore.Exceptions;
using GtMotive.Estimate.Microservice.Domain;
using GtMotive.Estimate.Microservice.Domain.Entities;
using GtMotive.Estimate.Microservice.Domain.Interfaces;

namespace GtMotive.Estimate.Microservice.ApplicationCore.UseCases.Rentals
{
    /// <summary>
    /// Starts a rental, relying on SQLite unique indexes to arbitrate concurrent requests.
    /// </summary>
    public sealed class RentVehicleUseCase(
        IVehicleRepository vehicles,
        IRentalRepository rentals,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        /// <summary>
        /// Starts an active rental for an eligible vehicle and a free customer.
        /// </summary>
        /// <param name="vehicleId">Identifier of the vehicle to rent.</param>
        /// <param name="customerId">Stable customer identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The newly created active rental.</returns>
        public async Task<Rental> ExecuteAsync(
            Guid vehicleId,
            string customerId,
            CancellationToken cancellationToken = default)
        {
            if (vehicleId == Guid.Empty)
            {
                throw new DomainException("The vehicle identifier is required.");
            }

            if (string.IsNullOrWhiteSpace(customerId))
            {
                throw new DomainException("The customer identifier is required.");
            }

            var vehicle = await vehicles.GetByIdAsync(vehicleId, cancellationToken)
                ?? throw new NotFoundException("The vehicle does not exist.");

            var now = clock.GetUtcNow().UtcDateTime;
            var today = DateOnly.FromDateTime(now);
            if (!vehicle.IsEligibleForRental(today))
            {
                throw new DomainException("The vehicle exceeds the five-year rental age limit.");
            }

            // The constructor also normalizes the customer identifier for the uniqueness check.
            var rental = Rental.Start(Guid.NewGuid(), vehicleId, customerId, now);
            if (await rentals.HasActiveRentalForCustomerAsync(rental.CustomerId, cancellationToken))
            {
                throw new ConflictException("The customer already has an active rental.");
            }

            if (await rentals.HasActiveRentalForVehicleAsync(vehicleId, cancellationToken))
            {
                throw new ConflictException("The vehicle is already rented.");
            }

            // These pre-checks give friendly errors; the database indexes close the race.
            await rentals.AddAsync(rental, cancellationToken);
            await unitOfWork.Save();
            return rental;
        }
    }
}
