# MeniSpot

MeniSpot is a full-stack digital menu platform for restaurants, cafes, and bars. It helps hospitality businesses publish a QR-accessible menu, manage food and drink items, update daily offers, control visibility, and track public menu views from a dedicated owner dashboard.

The platform includes a central administration panel for managing restaurants, subscriptions, QR codes, themes, and a reusable global drink library.

![MeniSpot landing page](docs/screenshots/landing.png)

## Features

- QR-based public menus without requiring a mobile app
- Super admin dashboard for restaurants, licenses, themes, and QR code management
- Restaurant owner dashboard with menu readiness checks and weekly menu view analytics
- Food and drink menu separation for cleaner public browsing
- Global drink library to reuse product data and images across restaurants
- Daily menu and special offer management
- Restaurant-specific branding, logo, cover image, working hours, and menu theme
- Image upload support with WebP conversion and compression
- Authentication with role-based access for platform admin and restaurant owners
- Docker-based local and production setup

## Tech Stack

- Angular 20
- TypeScript
- ASP.NET Core Web API on .NET 10
- Entity Framework Core
- PostgreSQL
- ASP.NET Core Identity
- JWT authentication
- Docker and Docker Compose
- xUnit

## Project Status

MeniSpot is under active development and currently supports the core digital menu workflow, restaurant administration, owner menu management, QR menu access, image uploads, reusable drink library items, and basic public menu analytics.

## Screenshots

### Public Landing And Authentication

The landing page introduces the platform and includes a contact form for new restaurant inquiries. Restaurant owners access the system through a dedicated login page.

![Landing page](docs/screenshots/landing.png)

![Login page](docs/screenshots/login.png)

### Platform Administration

The super admin area is used to manage the whole platform from one place: restaurants, subscriptions, themes, QR codes, and the global drink library.

![Admin dashboard](docs/screenshots/admin-dashboard.png)

![Restaurant management](docs/screenshots/admin-restaurants.png)

![Create restaurant form](docs/screenshots/admin-restaurant-form-1.png)

![Restaurant access and details](docs/screenshots/admin-restaurant-form-2.png)

### Global Drink Library

The global drink library allows the platform administrator to add common drinks once and reuse them across multiple restaurants. Owners can then select drinks, choose serving sizes, and only enter their own prices.

![Admin drink library](docs/screenshots/admin-drink-library.png)

![Drink library form](docs/screenshots/admin-drink-library-form.png)

### Restaurant Owner Dashboard

Each restaurant owner gets a dedicated dashboard for managing the public menu, checking menu readiness, previewing the menu, and tracking weekly QR menu views.

![Owner dashboard](docs/screenshots/owner-dashboard.png)

![Owner analytics](docs/screenshots/owner-dashboard-analytics.png)

### Menu Management

Owners can create categories, add custom products, import drinks from the global library, manage daily menus, and publish special offers.

![Owner categories](docs/screenshots/owner-categories.png)

![Create category](docs/screenshots/owner-category-form.png)

![Owner products](docs/screenshots/owner-products.png)

![Add product form](docs/screenshots/owner-product-form.png)

![Import drinks from library](docs/screenshots/owner-drink-library-picker.png)

![Daily menu](docs/screenshots/owner-daily-menu.png)

![Special offers](docs/screenshots/owner-special-offers.png)

### Restaurant Settings And QR Code

Restaurant owners can update contact information, branding, menu theme, working hours, and download a printable QR code.

![Owner settings](docs/screenshots/owner-settings.png)

![Business hours](docs/screenshots/owner-business-hours.png)

![QR code](docs/screenshots/owner-qr-code.png)

### Public Digital Menu

Guests scan the QR code and open a fast, responsive public menu. Food and drinks are separated for easier browsing, while daily menus and special offers stay highlighted.

![Public menu desktop](docs/screenshots/public-menu-desktop-home.png)

![Public food menu desktop](docs/screenshots/public-menu-desktop-food.png)

![Public drinks menu desktop](docs/screenshots/public-menu-desktop-drinks.png)

![Public menu mobile](docs/screenshots/public-menu-mobile-home.png)

![Public food menu mobile](docs/screenshots/public-menu-mobile-food.png)

![Public drinks menu mobile](docs/screenshots/public-menu-mobile-drinks.png)

## Project Structure

```text
frontend/
  Angular application

backend/
  DigitalMenu.Domain          Domain entities and business rules
  DigitalMenu.Application     Contracts and service interfaces
  DigitalMenu.Infrastructure  EF Core, Identity, JWT, services, migrations
  DigitalMenu.Api             HTTP API and application configuration
  DigitalMenu.Tests           Automated tests

deploy/
  Production deployment files

docs/
  Project screenshots and documentation assets
```

## Prerequisites

- Docker and Docker Compose
- .NET 10 SDK
- Node.js
- pnpm

## Local Development

Start the full local development stack:

```powershell
.\start-dev.ps1
```

The script:

1. Starts the local PostgreSQL database through Docker Compose
2. Runs the ASP.NET Core API
3. Starts the Angular development server

Stop the backend and frontend dev processes:

```powershell
.\stop-dev.ps1
```

The stop script leaves PostgreSQL running so the local database stays ready for the next test session.

### Database

Start the local PostgreSQL database in Docker:

```powershell
docker compose up -d postgres
```

Check or stop the database:

```powershell
docker compose ps
docker compose down
```

Local database connection:

```text
Host: localhost
Port: 5432
Database: digital_menu
Username: postgres
Password: postgres
```

> [!WARNING]
> These database credentials are intended only for local development. Never use default credentials in production.

### Backend

```powershell
dotnet run --project backend/DigitalMenu.Api
```

Swagger is available at `/swagger` in the Development environment. Database migrations and development administrator seeding run automatically when the API starts.

Development administrator credentials:

```text
Email: admin@admin.com
Password: admin
```

> [!WARNING]
> These credentials are intended for local development only. Production credentials must be provided through environment variables or a secure secret store.

### Frontend

```powershell
cd frontend
pnpm start
```

The Angular application is available at:

```text
http://localhost:4200
```

## Environment Configuration

Production configuration is based on environment variables. Use `.env.production.example` as a template and replace every placeholder value before deployment.

```env
POSTGRES_DB=menispot
POSTGRES_USER=menispot
POSTGRES_PASSWORD=change-this-postgres-password

Jwt__Issuer=DigitalMenu.Api
Jwt__Audience=DigitalMenu.Web
Jwt__Key=change-this-to-a-long-random-secret-with-at-least-32-characters
Jwt__ExpirationMinutes=60

SeedAdmin__Email=admin@example.com
SeedAdmin__Password=change-this-admin-password

AllowedOrigins__0=https://your-domain.com
```

Never commit real production secrets, database passwords, JWT signing keys, administrator credentials, SMTP credentials, or external service API keys.

## Security

- JWT-based authentication
- Role-based authorization
- Password hashing through ASP.NET Core Identity
- Environment-based production secrets
- PostgreSQL runs inside the Docker network in production
- Uploaded image validation with WebP conversion and compression

Additional production security notes are documented in [docs/SECURITY.md](docs/SECURITY.md).

## Production Deployment

The project includes Docker Compose production configuration for running the Angular frontend, ASP.NET Core API, PostgreSQL database, and Caddy reverse proxy on a VPS.

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build
```

Before deploying:

1. Copy `.env.production.example` to `.env.production`
2. Replace all placeholder secrets
3. Configure the public domain and allowed origins
4. Ensure ports 80 and 443 are open
5. Never expose PostgreSQL port 5432 publicly

## Backups

Production backup instructions are documented in [docs/BACKUPS.md](docs/BACKUPS.md).

The backup script creates a PostgreSQL dump and archives uploaded images from the production container:

```bash
cd /opt/menispot
chmod +x deploy/backup-prod.sh
./deploy/backup-prod.sh
```

Backups must stay outside the Git repository because they may contain customer data and uploaded files.

## Testing

Run backend tests:

```powershell
dotnet test DigitalMenu.slnx
```

Build frontend:

```powershell
cd frontend
npm run build
```
