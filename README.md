# GT Motive — Vehicle Renting Microservice

A local-first REST API for managing a vehicle renting fleet. This implementation adapts the supplied hexagonal-architecture template and implements all four requested use cases: register a vehicle, list available vehicles, start a rental, and return a rental.

**Technology:** .NET 9 / ASP.NET Core, EF Core 9, SQLite, xUnit and Swagger. No database server, Docker daemon, cloud account, or external identity service is required to run the API in **Development** mode.

## Quick start (Windows PowerShell)

### Requirements

- Git (to clone the repository).
- **.NET SDK 9.0.200**, exactly: `global.json` disables SDK roll-forward. Check with `dotnet --version` from the repository root.
- Network access to NuGet.org may be required on the first `dotnet restore`. Once dependencies are restored, no external *runtime* service is needed in Development mode.
- Free local TCP port **5080** for the commands below.

From a directory where you want to keep the project:

```powershell
git clone https://github.com/felixjimcal123/hexagonal-architecture-doc-main.git
cd .\hexagonal-architecture-doc-main
dotnet --version
dotnet restore .\src\microservice.sln
dotnet build .\src\microservice.sln --no-restore
dotnet test .\src\microservice.sln --no-restore --no-build
```

Run the API **from the repository root**, in the same terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project .\src\GtMotive.Estimate.Microservice.Host --no-build -- --urls http://localhost:5080
```

Open **http://localhost:5080/swagger**. Stop the process with `Ctrl+C`.

> The `--no-build` flags assume that the preceding build succeeded. If the build fails, fix that failure before running the tests; `dotnet test --no-build` can otherwise use older assemblies.
>
> The Swagger **Authorize** control is inherited from the template. The four renting endpoints can be exercised in the verified local Development configuration without obtaining an external OAuth token.

## API contract

All request bodies use `application/json`. The Host exposes the following routes:

| Method | Route | Purpose | Success | Notable errors |
| --- | --- | --- | --- | --- |
| `POST` | `/api/vehicles` | Register a vehicle | `201 Created` | `400` invalid data/age; `409` duplicate plate |
| `GET` | `/api/vehicles/available` | List eligible vehicles without an active rental | `200 OK` | — |
| `POST` | `/api/rentals` | Start a rental | `201 Created` | `400` invalid input/age; `404` vehicle missing; `409` customer/vehicle busy |
| `POST` | `/api/rentals/{id}/return` | Return a rental by **rental ID** | `200 OK` | `404` rental missing; `409` already returned/concurrent update |

### Register and list vehicles

`POST /api/vehicles`:

```json
{
  "licensePlate": "ABC1234",
  "brand": "Toyota",
  "model": "Corolla",
  "manufacturedOn": "2024-06-15"
}
```

A successful response contains the generated `id` and the four submitted vehicle fields (with plate/strings normalized as explained below). Copy the returned `id` for the rental request. `201` deliberately does **not** include a `Location` header: there is no GET-by-ID endpoint.

`GET /api/vehicles/available` returns a JSON array of vehicle objects with `id`, `licensePlate`, `brand`, `model`, and `manufacturedOn`. It is empty when no vehicle qualifies.

### Rent and return

`POST /api/rentals` (replace the example UUID with the vehicle `id` obtained above):

```json
{
  "vehicleId": "00000000-0000-0000-0000-000000000001",
  "customerId": "customer-001"
}
```

A successful response has `id` (**rental ID**), `vehicleId`, `customerId`, `startedAtUtc`, and `returnedAtUtc` (`null` while active). Keep the returned rental `id`.

Call `POST /api/rentals/{id}/return` with that **rental ID** in the URL and no request body. The response includes the same rental with a non-null `returnedAtUtc`. The vehicle then becomes available again, provided it still meets the age rule.

For a complete smoke test in Swagger: create a vehicle (`201`), list it (`200`), rent it (`201`), verify that it disappears from the available list, try renting it again (`409`), return the rental (`200`), and verify that it reappears. Business errors use `ProblemDetails`.

## Business rules and explicit assumptions

- **Maximum fleet age:** a vehicle may be registered only if its manufacturing date is not in the future and `manufacturedOn.AddYears(5) >= today`. The exact fifth anniversary **is accepted**; the next day is too old. `today` is the current **UTC calendar date**, supplied through `TimeProvider` for testability. The same eligibility rule applies when listing or renting, so an older registered vehicle can cease to be available over time.
- **Available** means both age-eligible **and** having no active rental. This is not a separate stored status.
- **One active rental per customer; one per vehicle.** An active rental has `returnedAtUtc == null`. The customer's identifier is supplied by the caller in this local exercise; no authenticated customer registry or reservation API is implemented.
- **Duplicate plate** registration is rejected. Plates are normalized by removing whitespace and converting to uppercase; brand and model are trimmed. Customer IDs are trimmed and uppercased, so casing and leading/trailing spaces do not bypass rental uniqueness.
- **Returning** a rental sets a UTC timestamp, preserves the history row and releases active-rental restrictions. Returning it again is a `409 Conflict`. The return URL contains a rental ID, not the vehicle ID.
- **Identity and scope:** vehicle IDs and rental IDs are server-generated UUIDs. There are no payments, booking dates, or standalone reservation lifecycle; a rental starts immediately.

## Architecture

The original solution organization is retained:

```text
Host (composition root / runtime / Swagger)
  └─ Api (HTTP contracts, controllers, validation, error mapping)
       └─ ApplicationCore (create/list/rent/return use cases)
            └─ Domain (Vehicle, Rental, rules, persistence ports)

Infrastructure (EF Core SQLite adapters, repositories, unit of work)
  └─ implements Domain ports and is wired by Host
```

The typical request path is **HTTP -> Controller -> Use case -> Domain entity / repository port -> EF Core adapter -> SQLite**. The Host assembles those dependencies. ApplicationCore has no dependency on ASP.NET Core controllers or the database implementation. Domain entities enforce their own state/validation rules; use cases coordinate queries and saves; persistence enforces the cross-record constraints.

Main projects:

| Project | Responsibility |
| --- | --- |
| `GtMotive.Estimate.Microservice.Host` | Application startup, configuration, dependency injection, Swagger and database initialization |
| `GtMotive.Estimate.Microservice.Api` | REST routes, DTOs, model validation and `ProblemDetails` responses |
| `GtMotive.Estimate.Microservice.ApplicationCore` | Four application use cases, application-level conflict/not-found semantics |
| `GtMotive.Estimate.Microservice.Domain` | Vehicle/Rental business invariants and repository/unit-of-work interfaces |
| `GtMotive.Estimate.Microservice.Infrastructure` | SQLite EF Core context, repositories, persistence adapters and logging/telemetry implementations |

## Persistence and consistency

Development uses the `ConnectionStrings:Renting` setting from `appsettings.Development.json`, configured as **`Data Source=renting.db`**. This is a **file-backed SQLite** database, not EF Core's in-memory provider. The relative file path is resolved from the **process working directory**: follow the quick start from the same repository directory on subsequent runs to preserve the same data. Do not delete `renting.db` if you want to retain the state.

On startup the Host calls `EnsureCreatedAsync()` to create a missing schema. It does not apply EF Core migrations, so **schema evolution is not implemented**.

Three database unique indexes provide a second line of defense beyond application pre-checks:

1. `Vehicles.LicensePlate` is unique.
2. `Rentals.VehicleId` is unique **only for active rentals** (`ReturnedAtUtc IS NULL`).
3. `Rentals.CustomerId` is unique **only for active rentals** (same filter).

This permits rental history while blocking simultaneous active duplicates. `RentingUnitOfWork` translates SQLite unique-constraint violations into application conflicts (`409` at the HTTP boundary). The return timestamp is an EF Core concurrency token to detect stale overlapping returns. SQLite uses its own locking rules and is a deliberate portability trade-off, **not** a claim of high-throughput concurrent writes.

The repository `.gitignore` excludes the local `renting.db` file and its `-wal` / `-shm` companions in the expected launch locations. Database content is not intended to be committed.

## Automated tests

From the repository root:

```powershell
dotnet test .\src\microservice.sln
```

| Test project | Tests in verified checkpoint | What it checks |
| --- | ---: | --- |
| `test/unit/GtMotive.Estimate.Microservice.UnitTests` | 32 | Domain/use-case behavior in isolation, without production database or HTTP host |
| `test/functional/GtMotive.Estimate.Microservice.FunctionalTests` | 13 | Use cases with real SQLite-backed adapters, **without** an HTTP host; persistence, history, constraints and return concurrency |
| `test/infrastructure/GtMotive.Estimate.Microservice.InfrastructureTests` | 4 | Test host receiving HTTP requests and validating request models, not full end-to-end business flows |
| **Total** | **49** | **49 passed, 0 failed, 0 skipped** in the clean-clone checkpoint |

The three categories deliberately match the definitions in the original technical-test PDF. There is also a separately verified manual HTTP lifecycle, and a manual test showing that a vehicle survives a complete Host stop/restart. Neither is misrepresented as an additional automated test.

## Decisions, costs and limits

| Problem | Decision | Benefit | Cost / mitigation |
| --- | --- | --- | --- |
| Evaluator must run locally | SQLite + EF Core 9 | No separate database service | SQLite writer contention; database indexes guard the rental invariants |
| Duplicate rentals under races | Friendly pre-checks **and** filtered unique indexes | Readable errors and database-enforced exclusivity | SQLite constraints become `409`; no performance/load guarantee |
| Reusable business logic | Use cases + domain ports + repository/unit of work | Testable without the HTTP host | Some indirection; reused the supplied template rather than adding a framework |
| Repeatable date rules | Inject `TimeProvider` | Deterministic boundary tests | Application relies on a correct UTC clock source |
| Minimal local setup | `EnsureCreatedAsync()` | Automatically creates a new database | No schema migration strategy; use migrations before production evolution |
| Limited delivery scope | Existing five production project layers, no frontend/cloud integration | Focus on required behavior and testing | Authentication/authorization, operations and deployment hardening remain outside the exercise |

**Other limitations:** There is no vehicle GET-by-ID or rental-history REST endpoint, no booking/payment lifecycle, no pagination, and no authenticated link between `customerId` and a real user. Application error logging still logs expected conflicts at an unnecessarily high severity. These are known trade-offs rather than implemented features.

**Deployment boundary:** Run in **Development** for this take-home exercise. The inherited non-Development template configuration references Azure Key Vault, Application Insights and an identity provider. The inherited `Dockerfile` has an older SDK image and private-feed credentials setup; **Docker build and production deployment have not been verified and are not required for the supported local execution path**. NuGet restoration for the local solution uses `NuGet.config` with NuGet.org.

## Reproducibility evidence

The delivery checkpoint was verified from a fresh GitHub clone at commit **`780f027`** using SDK **9.0.200**: restore succeeded, all eight solution projects built, and **49/49 automated tests passed**. Running the Host on `http://localhost:5080` exposed all four Swagger operations. A vehicle created through HTTP remained present with the same identifier after the Host was stopped and restarted using the same working directory.
