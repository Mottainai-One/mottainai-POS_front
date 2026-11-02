# Mottainai POS Frontend

Windows desktop point-of-sale application built with WPF and .NET 10.

## Requirements

- Windows
- .NET 10 SDK

## Build

```powershell
dotnet build PDVMottainai.slnx
```

Configure the POS API before starting the application.

## POS API integration

The desktop client connects to the local POS API at `http://127.0.0.1:5097/` by default. Configure these process environment variables before starting the application:

| Variable | Purpose |
|---|---|
| `POS_API_BASE_URL` | Optional local API base URL. |
| `POS_FIREBASE_API_KEY` | Firebase Web API key used to exchange the operator's email and password for a Firebase ID token. |

The API must also be configured with the Firebase project, terminal, and operational database settings described in the POS API README. Login uses Firebase authentication and then exchanges the selected terminal for a local POS session. Product, shift, draft sale, cash movement, approval, promotion, and sales summary requests use the authenticated POS API.

The API currently exposes checkout as a simulation only. Payment simulations do not persist payments or complete a sale. Customer lookup remains unavailable in this desktop build; the POS screen does not send CPF data to the API.
