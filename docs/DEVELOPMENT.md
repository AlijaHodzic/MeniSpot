# MeniSpot Development Notes

Ovaj dokument opisuje zadnje dodane tokove i kako ih koristiti tokom lokalnog testiranja.

## Brzo pokretanje lokalno

Iz root foldera projekta:

```powershell
.\start-dev.ps1
```

Skripta radi sljedece:

- pokrece PostgreSQL kroz Docker Compose
- pokrece backend API na `http://localhost:5158`
- pokrece Angular frontend na `http://localhost:4200`
- sprema logove u `.dev/logs`
- sprema PID procese u `.dev/processes.json`

Kad zavrsis testiranje:

```powershell
.\stop-dev.ps1
```

Ova skripta gasi backend i frontend dev procese. PostgreSQL Docker container ostaje upaljen da ne moras svaki put ponovo dizati bazu.

Ako zelis ugasiti i bazu:

```powershell
docker compose down
```

## Admin ulazak kao vlasnik restorana

U admin panelu, u tabu `Restorani`, svaki restoran ima dugme:

```text
Otvori kao vlasnik
```

Kad kliknes:

- admin sesija se privremeno sacuva u browseru
- aplikacija te prebaci u vlasnicki panel tog restorana
- nije potrebna vlasnikova lozinka
- u vlasnickom panelu se pojavi dugme `Nazad u admin`

Ovo sluzi za podrsku klijentima, testiranje njihovog menija i brzu provjeru sta vlasnik vidi.

## Zaboravljena lozinka vlasnika

Trenutni MVP tok je administrativni reset:

1. Otvori admin panel.
2. Idi na `Restorani`.
3. Klikni `Uredi` za restoran.
4. U sekciji `Vlasnicki pristup` upisi novu privremenu lozinku.
5. Sacuvaj promjene.
6. Vlasnik se prijavi s novom lozinkom.

Stara lozinka se ne prikazuje i ne cuva u citljivom obliku. To je sigurnije i profesionalnije.

Kasniji profesionalni tok:

- dodati email servis
- dodati `Zaboravljena lozinka` formu
- vlasniku poslati reset link na email
- link vrijedi ograniceno vrijeme

## UI promjene

Admin lista restorana je zategnuta:

- akcije su grupisane u desni blok
- dodan je ulaz u vlasnicki panel
- reset lozinke je objasnjen direktno u edit modalu

Vlasnicki dashboard vise ne prikazuje prototipne analytics brojke:

- prikazuje status menija
- broj proizvoda
- broj kategorija
- aktivnu temu
- checklistu spremnosti menija

Public meni je ociscen:

- uklonjeno je `Demo` dugme
- dodano je prazno stanje kad nema proizvoda za prikaz

## Sta jos nije automatski rijeseno

Email reset lozinke nije implementiran jer jos nije izabran email provider. Za produkciju bi trebalo izabrati npr. Resend, SMTP provider ili neki drugi transactional email servis.
