<#
.SYNOPSIS
    Creates a ready-to-use account in the local Cognito emulator (Moto) and prints its access token.
    PowerShell twin of cognito-local-signup.sh.

.DESCRIPTION
    Does what a user and staging Cognito do together: sign-up (the public call a frontend makes), confirm
    (real Cognito emails a code; locally the account is confirmed as admin), then join the group (staging's
    post-confirmation Lambda adds every new account to Bidder; Moto runs no triggers).

    Requires the AWS CLI v2 on PATH. Moto accepts any credentials, so dummy ones are used if none are set.

.EXAMPLE
    $token = .\scripts\cognito-local-signup.ps1 ann@example.com 'Correct-horse-42' Ann
    Invoke-RestMethod http://localhost:5081/api/v1/auctions -Method Post -Headers @{ Authorization = "Bearer $token" } `
        -ContentType 'application/json' `
        -Body '{"title":"Vintage camera","description":"Works","startingPrice":100,"currency":"USD","durationInHours":24}'

.EXAMPLE
    .\scripts\cognito-local-signup.ps1 boss@example.com 'Correct-horse-42' -Group Admin
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Email,
    [Parameter(Mandatory, Position = 1)][string]$Password,
    [Parameter(Position = 2)][string]$DisplayName,
    [ValidateSet('Bidder', 'Admin', 'none')][string]$Group = 'Bidder',
    [string]$Endpoint = $(if ($env:COGNITO_ENDPOINT) { $env:COGNITO_ENDPOINT } else { 'http://localhost:5555' })
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Same pool and app client as appsettings.Development.json (Cognito section) and docker/moto-init.sh.
$Pool = 'us-east-1_d85380f7cde7bc8c1b0ca2ed3ee5ddc1affc77945b39d'
$Client = '123b1f3863b67d1a3e89103c68'

if (-not $DisplayName) { $DisplayName = $Email.Split('@')[0] }
if (-not (Get-Command aws -ErrorAction SilentlyContinue)) {
    throw 'The AWS CLI v2 is required: https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html'
}

if (-not $env:AWS_ACCESS_KEY_ID) { $env:AWS_ACCESS_KEY_ID = 'testing' }
if (-not $env:AWS_SECRET_ACCESS_KEY) { $env:AWS_SECRET_ACCESS_KEY = 'testing' }
$env:AWS_DEFAULT_REGION = 'us-east-1'
$env:AWS_PAGER = ''

function Invoke-Cognito {
    # Windows PowerShell 5.1 turns a native command's stderr into a terminating error under 'Stop', so relax it
    # for the call and judge success by the exit code instead.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $output = & aws cognito-idp --endpoint-url $Endpoint @args 2>&1 }
    finally { $ErrorActionPreference = $previous }

    $text = (($output | ForEach-Object { "$_" }) -join "`n").Trim()
    if ($LASTEXITCODE -ne 0) { throw "aws cognito-idp $($args[0]) failed: $text" }
    return $text
}

$poolExists = $true
try { Invoke-Cognito describe-user-pool --user-pool-id $Pool | Out-Null } catch { $poolExists = $false }
if (-not $poolExists) {
    throw ("The local user pool is missing. Start the infra (docker compose -f docker/docker-compose.infra.yml up -d), " +
        'or re-seed a restarted Moto with: docker compose -f docker/docker-compose.infra.yml up moto-init')
}

Invoke-Cognito sign-up --client-id $Client --username $Email --password $Password `
    --user-attributes "Name=email,Value=$Email" "Name=name,Value=$DisplayName" | Out-Null
Invoke-Cognito admin-confirm-sign-up --user-pool-id $Pool --username $Email | Out-Null

if ($Group -ne 'none') {
    Invoke-Cognito admin-add-user-to-group --user-pool-id $Pool --username $Email --group-name $Group | Out-Null
}

Write-Host "Created $Email (group: $Group)"

# Only the token goes to the output stream, so `$token = .\scripts\cognito-local-signup.ps1 ...` captures it.
Invoke-Cognito initiate-auth --client-id $Client --auth-flow USER_PASSWORD_AUTH `
    --auth-parameters "USERNAME=$Email,PASSWORD=$Password" `
    --query AuthenticationResult.AccessToken --output text
