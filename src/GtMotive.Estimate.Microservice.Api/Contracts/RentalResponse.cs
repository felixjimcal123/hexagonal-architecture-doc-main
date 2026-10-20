using System;
using GtMotive.Estimate.Microservice.Domain.Entities;

namespace GtMotive.Estimate.Microservice.Api.Contracts
{
    /// <summary>
    /// Public representation of a rental and its status.
    /// </summary>
    public sealed record RentalResponse(
        Guid Id,
        Guid VehicleId,
        string CustomerId,
        DateTime StartedAtUtc,
        DateTime? ReturnedAtUtc)
    {
        public static RentalResponse FromRental(Rental rental)
        {
            ArgumentNullException.ThrowIfNull(rental);
            return new RentalResponse(
                rental.Id,
                rental.VehicleId,
                rental.CustomerId,
                rental.StartedAtUtc,
                rental.ReturnedAtUtc);
        }
    }
}
