using System;
using System.Threading;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.Api.Contracts;
using GtMotive.Estimate.Microservice.ApplicationCore.UseCases.Rentals;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GtMotive.Estimate.Microservice.Api.Controllers
{
    /// <summary>
    /// Starts and completes rentals.
    /// </summary>
    [ApiController]
    [Route("api/rentals")]
    // Both commands belong to the same rental REST resource (same precedent as VehiclesController).
#pragma warning disable S6960
    public sealed class RentalsController(
        RentVehicleUseCase rentVehicle,
        ReturnVehicleUseCase returnVehicle) : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(typeof(RentalResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<RentalResponse>> Rent(
            [FromBody] CreateRentalRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var rental = await rentVehicle.ExecuteAsync(request.VehicleId!.Value, request.CustomerId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, RentalResponse.FromRental(rental));
        }

        [HttpPost("{id:guid}/return")]
        [ProducesResponseType(typeof(RentalResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<RentalResponse>> Return(Guid id, CancellationToken cancellationToken)
        {
            var rental = await returnVehicle.ExecuteAsync(id, cancellationToken);
            return Ok(RentalResponse.FromRental(rental));
        }
    }
#pragma warning restore S6960
}
