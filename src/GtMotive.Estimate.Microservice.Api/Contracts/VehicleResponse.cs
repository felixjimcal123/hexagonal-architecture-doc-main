using System;
using GtMotive.Estimate.Microservice.Domain.Entities;

namespace GtMotive.Estimate.Microservice.Api.Contracts
{
    /// <summary>
    /// Public representation of a registered vehicle.
    /// </summary>
    public sealed record VehicleResponse(Guid Id, string LicensePlate, string Brand, string Model, DateOnly ManufacturedOn)
    {
        public static VehicleResponse FromVehicle(Vehicle vehicle)
        {
            ArgumentNullException.ThrowIfNull(vehicle);
            return new VehicleResponse(vehicle.Id, vehicle.LicensePlate, vehicle.Brand, vehicle.Model, vehicle.ManufacturedOn);
        }
    }
}
