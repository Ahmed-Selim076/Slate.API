# Slate API — local setup

## 1. Start Postgres
```
docker compose up -d
```

## 2. Restore & run migrations
```
cd Slate.Api
dotnet restore
dotnet tool install --global dotnet-ef   # first time only
dotnet ef migrations add Initial
dotnet ef database update
```

## 3. Run the API
```
dotnet run
```
OpenAPI JSON (raw): `https://localhost:5001/openapi/v1.json` (port may vary — check the console output).
This just lists the endpoints as JSON for now; we can add a browsable UI (e.g. Scalar) later if useful.

## 4. Point the frontend at it
In the frontend project, once we wire up real API calls (replacing the `console.log` TODOs
in Login.tsx / Register.tsx / Dashboard.tsx / Board.tsx), point requests at this API's base URL,
e.g. `https://localhost:5001/api`.

## Before deploying
- Replace `Jwt:Key` in appsettings.json with a real random secret (32+ chars) — use
  `dotnet user-secrets` or environment variables, never commit a real key.
- Update `Cors:AllowedOrigins` to include your deployed frontend URL.
- Update `ConnectionStrings:Default` to your production Postgres (e.g. Railway).
