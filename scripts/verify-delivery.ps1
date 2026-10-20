# GT Motive -- reproducible local delivery verification.
# Windows PowerShell 5.1 compatible. No changes to application source or regular SQLite database.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repo = [System.IO.Path]::GetFullPath((Join-Path -Path $PSScriptRoot -ChildPath '..'))
$solution = Join-Path $repo 'src\microservice.sln'
$hostProject = Join-Path $repo 'src\GtMotive.Estimate.Microservice.Host'
$hostDll = Join-Path $hostProject 'bin\Debug\net9.0\GtMotive.Estimate.Microservice.Host.dll'
$runDir = Join-Path $env:TEMP ('gt-motive-delivery-' + [guid]::NewGuid().ToString('N'))
$resultsDir = Join-Path $runDir 'test-results'
$dbPath = Join-Path $runDir 'verification.db'
$script:hostProcess = $null
$script:hostAttempt = 0
$script:checks = 0
$originalEnv = @{
    ASPNETCORE_ENVIRONMENT = [Environment]::GetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Process')
    DOTNET_ENVIRONMENT = [Environment]::GetEnvironmentVariable('DOTNET_ENVIRONMENT', 'Process')
    ASPNETCORE_URLS = [Environment]::GetEnvironmentVariable('ASPNETCORE_URLS', 'Process')
    ConnectionStrings__Renting = [Environment]::GetEnvironmentVariable('ConnectionStrings__Renting', 'Process')
}

function Pass([string]$message) {
    $script:checks++
    Write-Host ('[PASS] ' + $message) -ForegroundColor Green
}

function Assert-Check([bool]$condition, [string]$message) {
    if (-not $condition) { throw ('FAILED: ' + $message) }
    Pass $message
}

function Invoke-Dotnet([string[]]$arguments) {
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw ('dotnet ' + ($arguments -join ' ') + ' failed with exit code ' + $LASTEXITCODE)
    }
}

function Get-TemporaryPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { return $listener.LocalEndpoint.Port }
    finally { $listener.Stop() }
}

function Invoke-Api([string]$method, [string]$path, $data = $null) {
    $uri = $script:baseUrl + $path
    $parameters = @{
        Uri = $uri
        Method = $method
        TimeoutSec = 15
        UseBasicParsing = $true
        Headers = @{ Accept = 'application/json' }
        ErrorAction = 'Stop'
    }
    if ($null -ne $data) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = ConvertTo-Json -InputObject $data -Depth 6 -Compress
    }
    $status = 0
    $raw = ''
    try {
        $response = Invoke-WebRequest @parameters
        $status = [int]$response.StatusCode
        $raw = [string]$response.Content
    }
    catch {
        $errorResponse = $_.Exception.Response
        if ($null -eq $errorResponse) { throw }
        $status = [int]$errorResponse.StatusCode
        $stream = $errorResponse.GetResponseStream()
        if ($null -ne $stream) {
            $reader = New-Object System.IO.StreamReader($stream)
            try { $raw = $reader.ReadToEnd() }
            finally { $reader.Dispose() }
        }
    }
    $body = $null
    if (-not [string]::IsNullOrWhiteSpace($raw)) {
        try { $body = ConvertFrom-Json -InputObject $raw -ErrorAction Stop }
        catch { $body = $null }
    }
    return [pscustomobject]@{ Status = $status; Body = $body; Raw = $raw }
}

function Assert-Status($result, [int]$expected, [string]$description) {
    if ($result.Status -ne $expected) {
        throw ("FAILED: $description; expected HTTP $expected, got HTTP $($result.Status); body: $($result.Raw)")
    }
    Pass ("$description -> HTTP $expected")
}

function Stop-VerificationHost {
    if ($null -ne $script:hostProcess) {
        try {
            if (-not $script:hostProcess.HasExited) {
                Stop-Process -Id $script:hostProcess.Id -Force -ErrorAction Stop
                $null = $script:hostProcess.WaitForExit(10000)
            }
        }
        finally {
            $script:hostProcess.Dispose()
            $script:hostProcess = $null
        }
    }
}

function Start-VerificationHost {
    $script:hostAttempt++
    $stdoutPath = Join-Path $runDir ("host-$($script:hostAttempt)-stdout.log")
    $stderrPath = Join-Path $runDir ("host-$($script:hostAttempt)-stderr.log")
    $port = Get-TemporaryPort
    $script:baseUrl = "http://127.0.0.1:$port"
    # Program uses WebApplication.CreateBuilder() without args: environment URL is authoritative.
    [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', $script:baseUrl, 'Process')

    # Start the built DLL directly: no `dotnet run` child process to leave behind.
    $args = @('"' + $hostDll + '"', '--urls', $script:baseUrl)
    $script:hostProcess = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList $args `
        -WorkingDirectory $hostProject -PassThru -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath
    $ready = $false
    for ($i = 0; $i -lt 120; $i++) {
        if ($script:hostProcess.HasExited) {
            throw ("Host exited during startup. See $stdoutPath and $stderrPath")
        }
        try {
            $response = Invoke-WebRequest -Uri ($script:baseUrl + '/swagger/index.html') `
                -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            if ([int]$response.StatusCode -eq 200 -and $response.Content -match 'swagger') {
                $ready = $true
                break
            }
        }
        catch { Start-Sleep -Milliseconds 250 }
    }
    if (-not $ready) { throw ("Host never became ready. See $stdoutPath and $stderrPath") }
    Pass ("Host started (PID $($script:hostProcess.Id)); Swagger HTTP 200")
}

Write-Host '=== GT MOTIVE: DELIVERY VERIFICATION ===' -ForegroundColor Cyan
Write-Host "Repository: $repo"
Write-Host "Reports:    $runDir"

$success = $false
try {
    New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null
    Push-Location $repo
    try {
        Assert-Check (Test-Path -LiteralPath $solution) 'Solution exists'
        Assert-Check (Test-Path -LiteralPath (Join-Path $repo 'global.json')) 'global.json exists'
        $version = (& dotnet --version | Select-Object -First 1).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'dotnet --version failed' }
        Assert-Check ($version -eq '9.0.200') 'SDK is exactly 9.0.200'

        Write-Host "`n[1/4] RESTORE" -ForegroundColor Cyan
        Invoke-Dotnet @('restore', $solution)
        Pass 'NuGet restore'

        Write-Host "`n[2/4] NON-INCREMENTAL BUILD / ANALYZERS" -ForegroundColor Cyan
        Invoke-Dotnet @('build', $solution, '--no-restore', '--no-incremental')
        Assert-Check (Test-Path -LiteralPath $hostDll) 'Eight-project solution build / Host DLL created'

        Write-Host "`n[3/4] UNIT + FUNCTIONAL + INFRASTRUCTURE TESTS" -ForegroundColor Cyan
        Invoke-Dotnet @('test', $solution, '--no-restore', '--no-build', '--logger', 'trx', '--results-directory', $resultsDir)
        $trxFiles = @(Get-ChildItem -LiteralPath $resultsDir -Filter '*.trx' -File)
        Assert-Check ($trxFiles.Count -eq 3) 'Three TRX test suites generated'
        $total = 0; $passed = 0; $failed = 0
        foreach ($trx in $trxFiles) {
            [xml]$document = Get-Content -LiteralPath $trx.FullName -Raw
            $counters = $document.SelectSingleNode('/*[local-name()="TestRun"]/*[local-name()="ResultSummary"]/*[local-name()="Counters"]')
            if ($null -eq $counters) { throw "TRX counters missing: $($trx.Name)" }
            $total += [int]$counters.GetAttribute('total')
            $passed += [int]$counters.GetAttribute('passed')
            $failed += [int]$counters.GetAttribute('failed')
        }
        Assert-Check ($total -eq 49 -and $passed -eq 49 -and $failed -eq 0) "Automated tests 49/49 (got $passed/$total; failed $failed)"

        Write-Host "`n[4/4] LIVE REST + SQLITE RESTART CHECK" -ForegroundColor Cyan
        [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Development', 'Process')
        [Environment]::SetEnvironmentVariable('DOTNET_ENVIRONMENT', 'Development', 'Process')
        [Environment]::SetEnvironmentVariable('ConnectionStrings__Renting', "Data Source=$dbPath", 'Process')

        Start-VerificationHost
        Assert-Check (Test-Path -LiteralPath $dbPath) 'Dedicated SQLite file created'

        $suffix = ([guid]::NewGuid().ToString('N')).Substring(0, 14).ToUpperInvariant()
        $plateA = 'QA' + $suffix + 'A'
        $plateB = 'QA' + $suffix + 'B'
        $customerA = 'QA-CUSTOMER-' + $suffix
        $manufactured = [DateTime]::UtcNow.AddYears(-2).ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
        $old = [DateTime]::UtcNow.AddYears(-7).ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)

        $invalid = Invoke-Api 'POST' '/api/vehicles' @{ licensePlate = ('OLD' + $suffix); brand = 'Test'; model = 'Old'; manufacturedOn = $old }
        Assert-Status $invalid 400 'Reject vehicle over five years old'

        $createA = Invoke-Api 'POST' '/api/vehicles' @{ licensePlate = $plateA; brand = 'Toyota'; model = 'Corolla'; manufacturedOn = $manufactured }
        Assert-Status $createA 201 'Create vehicle A'
        $vehicleA = [string]$createA.Body.id
        $parsedGuid = [guid]::Empty
        Assert-Check ([guid]::TryParse($vehicleA, [ref]$parsedGuid) -and $parsedGuid -ne [guid]::Empty) 'Vehicle A has a GUID'

        $duplicatePlate = Invoke-Api 'POST' '/api/vehicles' @{ licensePlate = $plateA; brand = 'Toyota'; model = 'Corolla'; manufacturedOn = $manufactured }
        Assert-Status $duplicatePlate 409 'Reject duplicate license plate'

        $createB = Invoke-Api 'POST' '/api/vehicles' @{ licensePlate = $plateB; brand = 'Ford'; model = 'Focus'; manufacturedOn = $manufactured }
        Assert-Status $createB 201 'Create vehicle B'
        $vehicleB = [string]$createB.Body.id
        $parsedGuid = [guid]::Empty
        Assert-Check ([guid]::TryParse($vehicleB, [ref]$parsedGuid) -and $parsedGuid -ne [guid]::Empty) 'Vehicle B has a GUID'

        $available = Invoke-Api 'GET' '/api/vehicles/available'
        Assert-Status $available 200 'List available vehicles'
        Assert-Check (@($available.Body | Where-Object { $_.id -eq $vehicleA -or $_.id -eq $vehicleB }).Count -eq 2) 'Both new vehicles initially available'

        $invalidRental = Invoke-Api 'POST' '/api/rentals' @{ vehicleId = $vehicleA; customerId = '' }
        Assert-Status $invalidRental 400 'Reject missing customer identity'

        $rental = Invoke-Api 'POST' '/api/rentals' @{ vehicleId = $vehicleA; customerId = $customerA }
        Assert-Status $rental 201 'Rent vehicle A'
        $rentalId = [string]$rental.Body.id
        $parsedGuid = [guid]::Empty
        Assert-Check ([guid]::TryParse($rentalId, [ref]$parsedGuid) -and $parsedGuid -ne [guid]::Empty) 'Rental has a GUID'
        Assert-Check ($null -eq $rental.Body.returnedAtUtc) 'Rental is active'

        $available = Invoke-Api 'GET' '/api/vehicles/available'
        Assert-Status $available 200 'List available after renting'
        Assert-Check (@($available.Body | Where-Object { $_.id -eq $vehicleA }).Count -eq 0) 'Rented vehicle excluded from availability'
        Assert-Check (@($available.Body | Where-Object { $_.id -eq $vehicleB }).Count -eq 1) 'Unrented vehicle remains available'

        $sameVehicle = Invoke-Api 'POST' '/api/rentals' @{ vehicleId = $vehicleA; customerId = ('OTHER-' + $suffix) }
        Assert-Status $sameVehicle 409 'Reject second active rental of same vehicle'
        $sameCustomer = Invoke-Api 'POST' '/api/rentals' @{ vehicleId = $vehicleB; customerId = $customerA }
        Assert-Status $sameCustomer 409 'Reject second active rental for same customer'

        $missingRental = Invoke-Api 'POST' ('/api/rentals/' + [guid]::NewGuid().ToString() + '/return')
        Assert-Status $missingRental 404 'Unknown rental returns 404'

        $returnResult = Invoke-Api 'POST' ('/api/rentals/' + $rentalId + '/return')
        Assert-Status $returnResult 200 'Return vehicle A'
        Assert-Check ($returnResult.Body.id -eq $rentalId -and $null -ne $returnResult.Body.returnedAtUtc) 'Return timestamp and rental history preserved'

        $repeatReturn = Invoke-Api 'POST' ('/api/rentals/' + $rentalId + '/return')
        Assert-Status $repeatReturn 409 'Reject repeated return'

        $available = Invoke-Api 'GET' '/api/vehicles/available'
        Assert-Status $available 200 'List available after return'
        Assert-Check (@($available.Body | Where-Object { $_.id -eq $vehicleA }).Count -eq 1) 'Returned vehicle becomes available'

        Write-Host 'Restarting the exact Host process while keeping the same SQLite file...' -ForegroundColor Cyan
        Stop-VerificationHost
        Start-VerificationHost
        $afterRestart = Invoke-Api 'GET' '/api/vehicles/available'
        Assert-Status $afterRestart 200 'List available after Host restart'
        Assert-Check (@($afterRestart.Body | Where-Object { $_.id -eq $vehicleA -and $_.licensePlate -eq $plateA }).Count -eq 1) 'Vehicle identity survives restart'
        Assert-Check (@($afterRestart.Body | Where-Object { $_.id -eq $vehicleB }).Count -eq 1) 'Second vehicle survives restart'
        $historicalReturn = Invoke-Api 'POST' ('/api/rentals/' + $rentalId + '/return')
        Assert-Status $historicalReturn 409 'Returned rental history survives restart'
        $rerent = Invoke-Api 'POST' '/api/rentals' @{ vehicleId = $vehicleA; customerId = $customerA }
        Assert-Status $rerent 201 'Same customer can rent again after previous return'

        & git diff --check
        if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed' }
        Pass 'git diff --check'

        $success = $true
        Write-Host "`n=== ALL CHECKS PASSED: $script:checks ===" -ForegroundColor Green
        Write-Host 'Unit, functional and infrastructure suites: 49/49; REST & persistence scenarios: PASS' -ForegroundColor Green
    }
    finally { Pop-Location }
}
catch {
    Write-Host "`n=== VERIFICATION FAILED ===" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    if ($_.ScriptStackTrace) { Write-Host $_.ScriptStackTrace -ForegroundColor DarkRed }
}
finally {
    Stop-VerificationHost
    foreach ($key in $originalEnv.Keys) {
        [Environment]::SetEnvironmentVariable($key, $originalEnv[$key], 'Process')
    }
    # Delete only the unique temporary SQLite database; preserve logs / TRX for diagnosis.
    foreach ($suffix in @('', '-wal', '-shm')) {
        $file = $dbPath + $suffix
        if (Test-Path -LiteralPath $file) {
            try { Remove-Item -LiteralPath $file -Force -ErrorAction Stop }
            catch { Write-Warning "Could not remove temporary database artifact: $file" }
        }
    }
    Write-Host "Reports saved under: $runDir"
}
if (-not $success) { exit 1 }
exit 0
