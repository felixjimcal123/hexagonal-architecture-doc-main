using System;
using System.ComponentModel.DataAnnotations;

namespace GtMotive.Estimate.Microservice.Api.Contracts
{
    /// <summary>
    /// Data accepted by the registration endpoint. The server generates the vehicle identifier.
    /// </summary>
    public sealed class CreateVehicleRequest
    {
        [Required]
        public string LicensePlate { get; set; } = string.Empty;

        [Required]
        public string Brand { get; set; } = string.Empty;

        [Required]
        public string Model { get; set; } = string.Empty;

        [Required]
        public DateOnly? ManufacturedOn { get; set; }
    }
}
