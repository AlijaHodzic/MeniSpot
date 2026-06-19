# MeniSpot

Platforma za digitalne menije restorana, kafića i barova s QR pristupom, prilagodljivim temama i jednostavnim upravljanjem ponudom.

## Tehnologije

- .NET 10 i ASP.NET Core Web API
- PostgreSQL i Entity Framework Core
- ASP.NET Core Identity i JWT
- xUnit
- Angular 20 i Tailwind CSS

## Lokalno pokretanje

### Backend

```powershell
docker compose up -d
$env:SeedAdmin__Email="admin@example.com"
$env:SeedAdmin__Password="ChangeMe123!"
dotnet run --project backend/DigitalMenu.Api
```

Swagger je u development okruženju dostupan na `/swagger`. Početna migracija se automatski primjenjuje pri pokretanju API-ja.

### Frontend

```powershell
cd frontend
pnpm start
```

Angular aplikacija je dostupna na `http://localhost:4200`.

Produkcijske vrijednosti za konekciju, JWT ključ i administratorski račun postavljaju se kroz environment varijable ili secret store. Razvojne vrijednosti iz `appsettings.json` ne koriste se u produkciji.

## Struktura

- `DigitalMenu.Domain`: entiteti i domenska pravila
- `DigitalMenu.Application`: API ugovori i servisni interfejsi
- `DigitalMenu.Infrastructure`: EF Core, Identity, JWT i implementacije servisa
- `DigitalMenu.Api`: HTTP rute i konfiguracija aplikacije
- `DigitalMenu.Tests`: automatizovani testovi
- `frontend`: Angular aplikacija
