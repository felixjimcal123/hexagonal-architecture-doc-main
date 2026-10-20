using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.Api.Contracts;
using GtMotive.Estimate.Microservice.ApplicationCore.UseCases.Vehicles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GtMotive.Estimate.Microservice.Api.Controllers
{
    /// <summary>
    /// Vehicle registration and availability queries.
    /// </summary>
    [ApiController]
    [Route("api/vehicles")]
    // Intentional: the commands and queries both operate on the same REST resource.
    // Splitting them solely to satisfy S6960 would add controllers without a domain boundary.
#pragma warning disable S6960
    public sealed class VehiclesController(
        CreateVehicleUseCase createVehicle,
        ListAvailableVehiclesUseCase listAvailableVehicles) : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<VehicleResponse>> Create(
            [FromBody] CreateVehicleRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            // ApiController ensures ManufacturedOn has a value before invoking this action.
            var vehicle = await createVehicle.ExecuteAsync(
                request.LicensePlate,
                request.Brand,
                request.Model,
                request.ManufacturedOn!.Value,
                cancellationToken);

            // No GET-by-id route exists yet; do not publish a misleading Location URI.
            return StatusCode(StatusCodes.Status201Created, VehicleResponse.FromVehicle(vehicle));
        }

        [HttpGet("available")]
        [ProducesResponseType(typeof(IReadOnlyList<VehicleResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<VehicleResponse>>> Available(CancellationToken cancellationToken)
        {
            var vehicles = await listAvailableVehicles.ExecuteAsync(cancellationToken);
            return Ok(vehicles.Select(VehicleResponse.FromVehicle).ToArray());
        }
    }
#pragma warning restore S6960
}
