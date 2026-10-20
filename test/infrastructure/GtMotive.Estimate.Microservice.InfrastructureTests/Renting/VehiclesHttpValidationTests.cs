using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.InfrastructureTests.Infrastructure;
using Xunit;

namespace GtMotive.Estimate.Microservice.InfrastructureTests.Renting
{
    [Collection("TestServer")]
    public sealed class VehiclesHttpValidationTests(GenericInfrastructureTestServerFixture fixture)
    {
        [Fact]
        public async Task RegistrationWithoutLicensePlateReturnsBadRequest()
        {
            using var client = fixture.Server.CreateClient();
            using var response = await client.PostAsJsonAsync("/api/vehicles", new
            {
                Brand = "Example",
                Model = "Model",
                ManufacturedOn = "2024-01-01",
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RegistrationWithoutManufacturingDateReturnsBadRequest()
        {
            using var client = fixture.Server.CreateClient();
            using var response = await client.PostAsJsonAsync("/api/vehicles", new
            {
                LicensePlate = "1234ABC",
                Brand = "Example",
                Model = "Model",
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
