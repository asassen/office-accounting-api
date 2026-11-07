#Requires -RunAsAdministrator
# Installs OfficeAccountingApi as a Windows service running as your own Windows account.
param(
    [string]$Exe = (Join-Path $PSScriptRoot 'bin\Release\net48\OfficeAccountingApi.exe'),
    [string]$Account = "$env:USERDOMAIN\$env:USERNAME",
    [switch]$UseAccount   # run as your account (needs a real password, not a PIN)
)
$name = 'OfficeAccountingApi'
if (-not (Test-Path $Exe)) { throw "Build first: dotnet build -c Release ($Exe not found)" }

$extra = @{}
if ($UseAccount) {
    $extra.Credential = Get-Credential -UserName $Account -Message 'Windows password for the account the service will run as'
}

if (Get-Service $name -ErrorAction SilentlyContinue) {
    Stop-Service $name -ErrorAction SilentlyContinue
    sc.exe delete $name | Out-Null
    Start-Sleep 2
}

New-Service -Name $name -BinaryPathName "`"$Exe`"" -DisplayName 'Office Accounting API' `
    -Description 'HTTP/JSON wrapper around the Office Accounting 2009 SDK' `
    -StartupType Automatic -DependsOn 'MSSQL$MSSMLBIZ' @extra | Out-Null

# Restart on failure after 10s, 30s, 60s; reset the counter after a day.
sc.exe failure $name reset= 86400 actions= restart/10000/restart/30000/restart/60000 | Out-Null
sc.exe config $name start= delayed-auto | Out-Null

Start-Service $name
Get-Service $name
Write-Host "Log: $env:ProgramData\OfficeAccountingApi\log.txt"
