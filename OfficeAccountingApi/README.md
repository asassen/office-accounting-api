# OfficeAccountingApi

HTTP/JSON wrapper around the Microsoft Office Accounting 2009 (UK) SDK.
.NET Framework 4.8, **x86** (the SDK assemblies are 32-bit). Must run on the PC where Office Accounting is installed.

## Configure
Edit `App.config` (copied to `bin\Release\net48\OfficeAccountingApi.exe.config`):

| Key | Meaning |
|---|---|
| `Server` | SQL instance, e.g. `.\MSSMLBIZ`. Company databases on it are discovered automatically. |
| `Database` | Default company when a request doesn't specify one (set it to your usual company, e.g. `sassenbusinesssolutions`). |
| `CompanyFile` | No longer used. |
| `ApiKey` | Sent by clients as the `X-Api-Key` header. |
| `Prefix` | Listen URL. Keep `localhost` and publish via Tailscale or Cloudflare Tunnel. |

The database must already be upgraded to the installed build (open it once in Office Accounting).

## Run
Console (testing): `bin\Release\net48\OfficeAccountingApi.exe`, press Enter to stop.

Windows service (run once from an **elevated** PowerShell; prompts for your Windows password):
```
dotnet build -c Release
.\install-service.ps1
```
It runs as your account (needed for SQL Server integrated auth), starts automatically after SQL Server, and restarts on failure. Logs: `%ProgramData%\OfficeAccountingApi\log.txt`. Remove with `sc.exe delete OfficeAccountingApi`. After editing `App.config`, copy it over `OfficeAccountingApi.exe.config` and `Restart-Service OfficeAccountingApi`.

## Choosing the company
Every request (except `/api/companies`) can pick a company with an `X-Company: <database>` header or `?company=<database>`. If omitted, the `Database` setting is the default. `GET /api/companies` lists the company databases found on the SQL instance. Companies open on first use and stay open; unknown names return 404.

## Endpoints (all require `X-Api-Key`)
- `GET /api/companies`
- `GET /api/health`
- `GET /api/customers`, `GET /api/customers/{guid}`
- `POST /api/customers` `{ "name", "email", "phone", "comments" }`
- `PUT /api/customers/{guid}` any of `name, email, phone, comments, active`
- `GET /api/vendors`, `/api/items`, `/api/invoices` (raw SDK data views)
- `GET /api/invoices/{guid}` – invoice with lines, `subTotal`, `tax`, `total`, `balance`
- `POST /api/invoices` – create a sales invoice (VAT is added from each item's tax group):
  `{"customerGuid":"…","date":"2026-11-07","dueDate":"…","reference":"…","lines":[{"itemGuid":"…","quantity":2,"unitPrice":5.62,"description":"…"}]}`
  `date`, `dueDate`, `reference`, `unitPrice` and `description` are optional; the date must fall in an open financial year.

## Notes
- All SDK calls run serially on one STA thread.
- Back up the company database before writing to it.
- Not implemented: delete/deactivate, invoice editing/voiding, purchase documents.
