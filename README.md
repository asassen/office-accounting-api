# Office Accounting 2009 (UK) API + MAUI client

Gives web and MAUI apps access to Microsoft Office Accounting 2009 running on a Windows PC.
The Office Accounting SDK is 32-bit .NET Framework only, so a small service on the accounting PC wraps it in HTTP/JSON.

```
MAUI / web app --HTTP+JSON--> OfficeAccountingApi (Windows service, x86 net48) --SDK--> SQL Express (.\MSSMLBIZ)
```

| Folder | What it is |
|---|---|
| `OfficeAccountingApi` | The service: console app or Windows service, per-request company selection (`X-Company` header), API-key auth |
| `OfficeAccountingApi.Client` | Typed .NET client library (`OfficeAccountingClient`) |
| `OfficeAccountingMaui` | MAUI app with service connection, company picker and customer list |

## Requirements
- Office Accounting 2009 and its SDK installed on the PC (default `C:\Program Files (x86)\Microsoft Small Business\`).
- .NET SDK 10 with the MAUI workload for the client/app; the service targets .NET Framework 4.8.
- Each company database must match the installed app build (4.0.3610). Open older ones once in the desktop app to upgrade.

## Quick start
1. Edit `OfficeAccountingApi\App.config`: set a random `ApiKey` (`CHANGE-ME` is not accepted), `Server` and default `Database`.
2. `cd OfficeAccountingApi; dotnet build -c Release`
3. Install as a service from an elevated PowerShell: `.\install-service.ps1` (runs as LocalSystem, no password needed; `-UseAccount` runs as your own account and needs a real password, not a PIN). Or just run the exe for console mode.
4. Check it: `curl.exe -H "X-Api-Key: <key>" http://localhost:5080/api/health`
5. Run the MAUI app (`cd OfficeAccountingMaui; dotnet build -f net10.0-windows10.0.19041.0`), enter the URL and key, pick a company.

Logs: `%ProgramData%\OfficeAccountingApi\log.txt`. See each folder's README for details.

## Security
- The service listens on localhost only. Publish it through Tailscale or a Cloudflare Tunnel; do not open router ports.
- Never commit your real `ApiKey`. Back up company databases before writing real data; create/update was only tested on the sample company.

## Not implemented
Delete/deactivate endpoints, invoice editing/voiding, purchase documents.
