#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.Domain.Entities;

namespace GtMotive.Estimate.Microservice.Domain.Interfaces
{
    /// <summary>
    /// Port for vehicle persistence and availability queries.
    /// </summary>
    public interface IVehicleRepository
    {
        /// <summary>
        /// Finds a vehicle by identifier.
        /// </summary>
        /// <param name="id">Vehicle identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The vehicle, or null when missing.</returns>
        Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a normalized license plate already exists.
        /// </summary>
        /// <param name="licensePlate">Normalized license plate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the license plate is already registered.</returns>
        Task<bool> ExistsByLicensePlateAsync(string licensePlate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists vehicles that meet the age limit and have no active rental.
        /// </summary>
        /// <param name="asOf">Calendar date for age eligibility.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Vehicles available for renting.</returns>
        Task<IReadOnlyList<Vehicle>> ListAvailableAsync(DateOnly asOf, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tracks a new vehicle for persistence.
        /// </summary>
        /// <param name="vehicle">The new vehicle.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task for the addition.</returns>
        Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
    }
}
