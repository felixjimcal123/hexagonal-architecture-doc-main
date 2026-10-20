using System;
using System.ComponentModel.DataAnnotations;

namespace GtMotive.Estimate.Microservice.Api.Contracts
{
    /// <summary>
    /// Request for starting a rental. Customer identity is supplied by the caller in this local exercise.
    /// </summary>
    public sealed class CreateRentalRequest
    {
        [Required]
        public Guid? VehicleId { get; set; }

        [Required]
        public string CustomerId { get; set; } = string.Empty;
    }
}
