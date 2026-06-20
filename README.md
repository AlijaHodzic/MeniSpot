# MeniSpot

MeniSpot is a digital menu platform for restaurants, cafes, and bars. It provides QR-based access, customizable themes, menu management, special offers, and subscription control from a central administration panel.

## Technology Stack

- Angular 20 and Tailwind CSS
- ASP.NET Core Web API on .NET 10
- PostgreSQL and Entity Framework Core
- ASP.NET Core Identity and JWT authentication
- xUnit

## Project Structure

- `frontend`: Angular application
- `backend/DigitalMenu.Domain`: domain entities and business rules
- `backend/DigitalMenu.Application`: contracts and service interfaces
- `backend/DigitalMenu.Infrastructure`: Entity Framework Core, Identity, JWT, and service implementations
- `backend/DigitalMenu.Api`: HTTP API and application configuration
- `backend/DigitalMenu.Tests`: automated tests

## Local Development

### Database

Start PostgreSQL with Docker:

```powershell
docker compose up -d
```

### Backend

```powershell
dotnet run --project backend/DigitalMenu.Api
```

Swagger is available at `/swagger` in the Development environment. The initial database migration is applied automatically when the API starts.

Development administrator credentials:

```text
Email: admin@admin.com
Password: admin
```

These credentials are intended for local development only. Production credentials must be provided through environment variables or a secure secret store.

### Frontend

```powershell
cd frontend
pnpm start
```

The Angular application is available at `http://localhost:4200`.

