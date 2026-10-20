using System;
using System.Threading;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.ApplicationCore.Exceptions;
using GtMotive.Estimate.Microservice.Domain.Entities;
using GtMotive.Estimate.Microservice.Domain.Interfaces;

namespace GtMotive.Estimate.Microservice.ApplicationCore.UseCases.Rentals
{
    /// <summary>
    /// Completes a rental while retaining its history.
    /// </summary>
    public sealed class ReturnVehicleUseCase(
        IRentalRepository rentals,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        /// <summary>
        /// Returns a vehicle by its rental identifier.
        /// </summary>
        /// <param name="rentalId">Identifier of the rental to close.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The returned rental including the return timestamp.</returns>
        public async Task<Rental> ExecuteAsync(Guid rentalId, CancellationToken cancellationToken = default)
        {
            var rental = await rentals.GetByIdAsync(rentalId, cancellationToken)
                ?? throw new NotFoundException("The rental does not exist.");

            if (!rental.IsActive)
            {
                throw new ConflictException("The rental has already been returned.");
            }

            rental.Return(clock.GetUtcNow().UtcDateTime);
            await unitOfWork.Save();
            return rental;
        }
    }
}
