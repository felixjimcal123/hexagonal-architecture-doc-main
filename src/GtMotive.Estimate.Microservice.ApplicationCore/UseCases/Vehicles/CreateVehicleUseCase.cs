using System;
using System.Threading;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.ApplicationCore.Exceptions;
using GtMotive.Estimate.Microservice.Domain.Entities;
using GtMotive.Estimate.Microservice.Domain.Interfaces;

namespace GtMotive.Estimate.Microservice.ApplicationCore.UseCases.Vehicles
{
    /// <summary>
    /// Registers a vehicle and persists it atomically through the unit of work.
    /// </summary>
    public sealed class CreateVehicleUseCase(
        IVehicleRepository vehicles,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        /// <summary>
        /// Validates and registers a new vehicle, rejecting a previously registered license plate.
        /// </summary>
        /// <param name="licensePlate">License plate of the new vehicle.</param>
        /// <param name="brand">Vehicle manufacturer.</param>
        /// <param name="model">Vehicle model.</param>
        /// <param name="manufacturedOn">Vehicle manufacturing date.</param>
        /// <param name="cancellationToken">Cancellation token for repository operations.</param>
        /// <returns>The registered vehicle.</returns>
        public async Task<Vehicle> ExecuteAsync(
            string licensePlate,
            string brand,
            string model,
            DateOnly manufacturedOn,
            CancellationToken cancellationToken = default)
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            var vehicle = Vehicle.Register(Guid.NewGuid(), licensePlate, brand, model, manufacturedOn, today);

            // Friendly error for sequential requests; the database's unique index closes the race.
            if (await vehicles.ExistsByLicensePlateAsync(vehicle.LicensePlate, cancellationToken))
            {
                throw new ConflictException("A vehicle with this license plate already exists.");
            }

            await vehicles.AddAsync(vehicle, cancellationToken);
            await unitOfWork.Save();
            return vehicle;
        }
    }
}
