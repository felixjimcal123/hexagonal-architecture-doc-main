using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.Domain.Entities;
using GtMotive.Estimate.Microservice.Domain.Interfaces;

namespace GtMotive.Estimate.Microservice.ApplicationCore.UseCases.Vehicles
{
    /// <summary>
    /// Lists vehicles eligible by age and without an active rental.
    /// </summary>
    public sealed class ListAvailableVehiclesUseCase(IVehicleRepository vehicles, TimeProvider clock)
    {
        /// <summary>
        /// Retrieves vehicles that have no active rental and satisfy the maximum-age rule.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the repository query.</param>
        /// <returns>The currently available vehicles.</returns>
        public Task<IReadOnlyList<Vehicle>> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            return vehicles.ListAvailableAsync(today, cancellationToken);
        }
    }
}
