#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.Domain.Entities;

namespace GtMotive.Estimate.Microservice.Domain.Interfaces
{
    /// <summary>
    /// Port for rental persistence. Active-rental uniqueness must be enforced atomically by the storage adapter.
    /// </summary>
    public interface IRentalRepository
    {
        /// <summary>
        /// Finds a rental by identifier.
        /// </summary>
        /// <param name="id">Rental identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The rental, or null when missing.</returns>
        Task<Rental?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether a customer currently has an active rental.
        /// </summary>
        /// <param name="customerId">Normalized customer identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if an active rental exists.</returns>
        Task<bool> HasActiveRentalForCustomerAsync(string customerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether a vehicle currently has an active rental.
        /// </summary>
        /// <param name="vehicleId">Vehicle identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if an active rental exists.</returns>
        Task<bool> HasActiveRentalForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tracks a new rental for persistence.
        /// </summary>
        /// <param name="rental">The new rental.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task for the addition.</returns>
        Task AddAsync(Rental rental, CancellationToken cancellationToken = default);
    }
}
