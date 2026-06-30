# MeniSpot

MeniSpot is a production digital menu platform for restaurants, cafes, and bars. It lets hospitality businesses publish a QR-accessible menu, manage food and drink items, update daily menus and offers, customize public menu branding, and track guest menu views from a dedicated owner dashboard.

The platform also includes a central administration panel for restaurant onboarding, subscription status management, QR code handling, themes, and a reusable global drink library.

**Live:** [menispot.com](https://menispot.com)

![MeniSpot landing page](docs/screenshots/landing.png)

## Product Highlights

- QR-based public menus without requiring a mobile app
- Public restaurant pages optimized for desktop and mobile guests
- Super admin dashboard for restaurants, licenses, themes, QR codes, and billing status
- Restaurant owner dashboard for menu management, readiness checks, and weekly view analytics
- Food and drink separation for cleaner public browsing
- Global drink library for reusable drink data, serving sizes, and images
- Daily menu and special offer management
- Restaurant-specific logo, cover image, contact details, working hours, and menu theme
- Image uploads with validation, WebP conversion, compression, and optimized public delivery
- Authentication with role-based access for platform admins and restaurant owners
- Docker-based production deployment with private PostgreSQL storage

## Production Architecture

MeniSpot is deployed as a containerized application behind Cloudflare and Caddy:

```text
Visitor
  |
  v
Cloudflare
  |
  v
Caddy reverse proxy
  |
  +--> Angular frontend
  |
  +--> ASP.NET Core API
          |
          v
      PostgreSQL
```

Cloudflare handles DNS, public proxying, and TLS at the edge. Caddy runs on the VPS as the reverse proxy and routes traffic to the frontend and API containers. PostgreSQL runs inside the Docker network and is not exposed publicly.

## Tech Stack

- Angular 20
- TypeScript
- ASP.NET Core Web API on .NET 10
- Entity Framework Core
- PostgreSQL
- ASP.NET Core Identity
- JWT authentication
- Docker and Docker Compose
- Caddy
- xUnit

## Screenshots

### Platform Landing

![Landing page](docs/screenshots/landing.png)

### Administration

![Admin dashboard](docs/screenshots/admin-dashboard.png)

![Restaurant management](docs/screenshots/admin-restaurants.png)

### Owner Dashboard

![Owner dashboard](docs/screenshots/owner-dashboard.png)

![Owner products](docs/screenshots/owner-products.png)

### Public Menu

![Public menu desktop](docs/screenshots/public-menu-desktop-home.png)

![Public menu mobile](docs/screenshots/public-menu-mobile-home.png)

More product screenshots are available in [docs/screenshots](docs/screenshots).

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
  Production deployment and backup files

docs/
  Development, deployment, security, backup, and screenshot documentation
```

## Local Development

Prerequisites:

- Docker and Docker Compose
- .NET 10 SDK
- Node.js
- pnpm or npm

Start the local development stack:

```powershell
.\start-dev.ps1
```

Stop backend and frontend development processes:

```powershell
.\stop-dev.ps1
```

The local development script starts PostgreSQL through Docker Compose, runs the ASP.NET Core API, and starts the Angular development server.

Detailed local development notes are documented in [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Environment Configuration

Production configuration is based on environment variables. Use [.env.production.example](.env.production.example) as a template and replace every placeholder value before deployment.

Never commit real production secrets, database passwords, JWT signing keys, administrator credentials, SMTP credentials, SSH keys, or external service API keys.

## Production Deployment

The production setup runs the Angular frontend, ASP.NET Core API, PostgreSQL database, and Caddy reverse proxy through Docker Compose on a VPS.

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build
```

Before deploying:

1. Create `.env.production` from `.env.production.example`
2. Replace all placeholder secrets with strong production values
3. Configure the public domain and allowed origins
4. Ensure ports `80` and `443` are open
5. Keep PostgreSQL private inside the Docker network

Full deployment instructions are documented in [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## Security

MeniSpot is designed so secrets, backups, uploaded files, and customer data stay outside the public repository.

Security practices include:

- JWT-based authentication
- Role-based authorization
- Password hashing through ASP.NET Core Identity
- Environment-based production secrets
- Private PostgreSQL container networking
- Uploaded image validation and compression
- Repository rules for excluding secrets, backups, and local files

Additional security notes are documented in [docs/SECURITY.md](docs/SECURITY.md).

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
npm run build --prefix frontend
```
