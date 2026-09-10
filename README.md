# GNIST ✳

Små spil. Store øjeblikke. En dansk multiplayer-festplatform, hvor TV'et er fællesskærmen, og telefonerne er controllere.

**ASP.NET Core / .NET 10 · Razor Pages · SignalR · C# · vanilla JavaScript · in-memory**

Den oprindelige produktbeskrivelse ligger uændret i [docs/BRIEF.md](docs/BRIEF.md).

## Start lokalt

Installér .NET 10 SDK. Åbn `minigames.sln` i Rider, eller kør fra projektets rod:

```sh
dotnet restore
dotnet run
```

**Åbn http://localhost:5180**. Adressen står også i terminalen. Ingen database, npm eller API-nøgler er nødvendige. Første restore henter NuGet-pakker; browserens SignalR-klient ligger allerede i projektet.

1. Tryk **Start en fest** på computeren.
2. Åbn en separat fane på `/join`, og indtast rumkoden og et navn. Gentag i flere faner for at simulere telefoner.
3. Vælg **Overrask os** for automatisk spil, eller vælg et bestemt spil.
4. Efter resultaterne går automatisk spil videre efter 10 sekunder. Værten kan stoppe automatikken eller gå tilbage til lobbyen.

En spiller kan være med alene til test; Duellen og Tikkende bombe kræver mindst to. Op til 100 spillere kan være i et rum. Nye spillere midt i en runde venter til næste runde.

### Telefoner på samme Wi-Fi

Bind bevidst til netværket:

```sh
dotnet run --no-launch-profile --urls http://0.0.0.0:5180
```

Find computerens lokale IP-adresse i netværksindstillingerne, fx `192.168.1.42`. **Åbn værtsiden via `http://192.168.1.42:5180`**, så QR-koden peger på en adresse, telefonerne kan nå. `localhost` på en telefon er telefonen selv. Tillad eventuelt port 5180 i computerens firewall. Begge enheder skal være på et netværk, der tillader trafik mellem enheder.

Hvis du allerede har oprettet rummet på localhost, er den enkleste løsning at oprette et nyt rum via LAN-adressen. Værtstokens gemmes pr. origin og fane. Produktion bruger almindelig HTTPS-hosting; LAN er kun en lokal udviklingsmulighed.

## De syv spil

| Spil | Handling og resultat |
| --- | --- |
| Klikamok | Flest servergodkendte klik på 10 sekunder. Værten kan vælge 5–60 sekunder. Maks. ét godkendt klik pr. 40 ms og dubletbeskyttelse. |
| På sekundet | Start/stop et skjult ur. Serveren måler afvigelsen fra 3, 5, 7 eller 10 sekunder. Nærmest vinder. |
| Lynhurtig | Vent på serverens tilfældige NU-signal. Tyvstart registreres; første forsøg er endeligt. |
| Hovedbrud | Dynamisk genereret plus, minus eller gange. Korrekte svar rangeres efter svartid; forkerte og manglende svar står nederst. |
| Duellen | To tilfældige spillere vælger hemmeligt sten, saks eller papir. Uafgjort giver omkamp. |
| Skæbnehjulet | Serveren vælger tilfældigt. Hjulets animation lander på den valgte spiller. |
| Tikkende bombe | Send bomben til en tilsluttet spiller før den skjulte serverstyrede lunte udløber. Kort afleveringspause; ingen øjeblikkelig retur ved 3+ spillere. |

De bedste gyldige placeringer i færdighedsspil får 3, 2 og 1 point. Ens resultater deler placering. Hjulet og bomben giver ingen færdighedspoint. Alle er med igen næste runde.

Konsekvenser er valgfrie: strafpoint, udfordring eller egen tekst. De gælder de nederste op til tre spillere (vinderne undtages), taberen af duellen, den udvalgte på hjulet eller den, der sidder med bomben. Der er ingen indbygget alkoholregel. Point og strafpoint holdes adskilt.

## Arkitektur

```text
Pages/                   Danske Razor Pages: forside, vært, telefon, fejl
Hubs/PartyHub.cs          SignalR-endepunkt, medlemskab og venlige fejl
Models/                  Rum, spillere, indstillinger, input og resultater
Services/RoomService.cs   Rum, unikke koder, tokens, forbindelser og udløb
Services/GameManager.cs   Rundeskift, resultater, point og quick play
Services/RoomBroadcaster.cs  Offentlige, værts- og private snapshots + timer
Games/                   IMiniGame, fælles livscyklus og syv implementeringer
wwwroot/css/             Responsivt design og bevægelse
wwwroot/js/              SignalR-klient, TV-visninger og telefoncontrollere
wwwroot/lib/             Lokal Microsoft SignalR JavaScript-klient
tests/Gnist.Tests/       Regeltests og rigtige SignalR-integrationstests
```

Et `Room` har en lås, så samtidige handlinger, afbrydelser og timeropdateringer behandles konsistent. Spillernes stabile ID'er er adskilt fra forbindelses-ID'er. `TimeProvider` gør tidslogikken testbar. Spil modtager serverens tid og accepterer aldrig browserberegnede point. Hvert input indeholder et runde-ID, så forsinkede handlinger fra sidste runde ignoreres.

Livscyklus: **Waiting (lobby) → Intro → Countdown → Playing → Finished → Results**. Spillene har en fast deltagerliste pr. runde. Ingen udgår permanent. Manglende svar bliver timeout-resultater; en afbrudt bombebærer erstattes af en tilsluttet deltager, hvis lunten stadig er aktiv.

### SignalR-flow

- `POST /api/rooms` opretter rum og returnerer et separat kryptografisk værtstoken. Kræver en brugerdefineret header og begrænser rumoprettelse pr. IP.
- `Host(code, token)` kontrollerer token på serveren og tilføjer forbindelsen til rummets værtsgruppe.
- `Join(code, name, playerToken?)` opretter eller genforbinder en spiller. Identiske navne får et nummer.
- `Start`, `Pause` og `Lobby` kræver et godkendt værtsmedlemskab. Et spillertoken giver aldrig værtsadgang.
- `Act` videresender input til `GameManager` og det aktive minispil.
- `State` er rummets offentlige tilstand; `HostState` indeholder TV-data; `Own` går kun til den enkelte spillers forbindelser. Tokens, skjulte svar, duelvalg og bombens lunte lækkes ikke i offentlige projektioner.
- Snapshots sendes fire gange pr. sekund. Under Lynhurtig sendes de op til 20 gange pr. sekund for et hurtigere NU-signal. Klik sendes enkeltvis til serveren, men den samlede rangliste sendes kun til værten; telefoner får deres eget antal.
- SignalR genforbinder automatisk efter netværksafbrydelser. Spilleren/værten identificerer sig igen med token fra `sessionStorage`. Faner kan derfor repræsentere forskellige spillere, mens en genindlæsning af samme fane bevarer identiteten. En duplikeret fane kan arve den oprindelige session; åbn en frisk fane til en ny testspiller.
- Hvis værten forsvinder, afsluttes den igangværende runde, men automatisk næste runde venter på værten.

### Tilføj et minispil

1. Opret en klasse i `Games/`, der arver `MiniGame`. Vælg varighed, og implementér `Input`, `PublicState` og `GetResults`; brug eventuelt `PrivateState` og `Update`.
2. Hold hemmelige data i serverfelter. Send kun bevidste projektioner til klienterne.
3. Registrér spillet i `GameCatalog.All` og factory-metoden `Create`.
4. Tilføj dansk titel, TV-visning og telefonkontrol i `wwwroot/js/app.js`.
5. Tilføj tests af regler, dubletter, tidsgrænser og eventuelle hemmeligheder.

Der er bevidst ingen database eller generisk repository-ramme. Ved senere persistens kan rumlagring udskilles fra `RoomService` og afsluttede resultater gemmes, når `GameManager` tildeler point. Spilklasserne behøver ikke kende databasen.

## Konfiguration

Indstillinger findes i `appsettings.json` og kan overskrives med miljøvariabler:

| Indstilling | Standard | Miljøvariabel |
| --- | --- | --- |
| Branding | GNIST | `Party__Brand` |
| Inaktivt rum udløber | 120 minutter | `Party__RoomIdleMinutes` |
| Maksimalt antal spillere pr. rum | 100 | `Party__MaxPlayers` |
| Maksimalt antal aktive rum | 500 | `Party__MaxRooms` |
| Offentlig URL til QR-koder | Aktuel request-origin | `Party__PublicBaseUrl` |

Indstil `Party__PublicBaseUrl` til den fulde HTTPS-adresse i produktion. Indstil også `AllowedHosts` til det/de rigtige hostnavne. Værten kan vælge spilvarighed og konsekvenser i lobbyen. Aktive spil og resultater forsvinder ved genstart/deployment.

## Test

```sh
dotnet build minigames.sln
dotnet test tests/Gnist.Tests/Gnist.Tests.csproj
```

Testene dækker alle sten/saks/papir-kombinationer, genereret regning/svarevaluering, timingrangering, tyvstart, klikbegrænsning, dubletter, hemmelige valg, bombens lunte, hjulets valg, rumoprettelse, navnekollisioner, genforbindelse, værtssikkerhed, sen tilmelding, udløb og automatisk spil.

Integrationstestene starter en rigtig ASP.NET Core-app i testserveren og kobler uafhængige SignalR-klienter på via long polling. De gennemfører **opret rum → QR → join → lobby → spil → resultater → genforbindelse → forlad** og kontrollerer Razor-ruterne. En rigtig 5-sekunders klikrunde køres med intro/nedtælling. Testene tager cirka 20 sekunder.

Til manuel browserkontrol: åbn en vært og to spillere, spil alle syv spil, genindlæs en spiller under en runde, test med en rigtig telefon og kontroller, at QR-koden kan scannes. Automatiske integrationstests erstatter ikke visuel kontrol på iPhone/Android.

## Azure App Service og GitHub

Der er to workflows: `.github/workflows/ci.yml` bygger/tester push og pull requests, og `azure.yml` bygger/tester/publicerer til Azure ved push til `main`, når Azure er konfigureret. Ingen cloudressourcer er oprettet af projektet.

1. Opret en Azure App Service med **.NET 10**, én instans og et passende App Service-abonnement. Aktivér HTTPS Only, WebSockets og Always On, hvor planen understøtter det. På Linux kan startup command være `dotnet Gnist.dll`.
2. Konfigurér `ASPNETCORE_ENVIRONMENT=Production`, `Party__PublicBaseUrl=https://DIT-NAVN.azurewebsites.net` og `AllowedHosts=DIT-NAVN.azurewebsites.net` i App Service. Tilpas ved eget domæne.
3. Opret Azure-login med OpenID Connect/federated credentials, afgrænset til GitHub-repositoryets `production`-environment. Giv identiteten den nødvendige adgang til den konkrete App Service.
4. Opret GitHub-environment `production`. Sæt repository-variable `AZURE_WEBAPP_NAME`. Sæt secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` og `AZURE_SUBSCRIPTION_ID` på repositoryet eller environmentet. Brug eventuelt environment-beskyttelse før deployment.
5. Push projektet til dit GitHub-repository på `main`. Workflowet kører test før `dotnet publish` og `azure/webapps-deploy`. Tjek derefter `/health`, opret et rum, og scan koden fra en telefon over mobilnettet.

**Hold MVP'en på én instans.** In-memory-rum deles ikke mellem processer. ARR affinity alene løser ikke, at forskellige spillere kan lande på forskellige servere. Før skalering skal rumtilstand og spilkoordinering deles, og SignalR skal have en passende backplane eller Azure SignalR Service. En deployment afbryder aktive fester.

Manuel publicering til en mappe:

```sh
dotnet publish Gnist.csproj -c Release -o artifacts/publish
```

Faglig reference: [Microsofts SignalR JavaScript-klient](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0), [SignalR-hosting og skalering](https://learn.microsoft.com/en-us/aspnet/core/signalr/scale?view=aspnetcore-10.0), [GitHub Actions til Azure App Service](https://learn.microsoft.com/en-gb/azure/app-service/deploy-github-actions?tabs=openid).

## MVP-begrænsninger

- Ingen varig lagring; rum er bevidst midlertidige. Lukker man en fane helt, kan dens session-token gå tabt.
- Timing og reaktion måles ved ankomst til serveren. Netværksforsinkelse påvirker derfor resultatet; spil på et stabilt netværk. Der er ingen browserberegnet eller klientrapporteret slutscore.
- Klikbegrænsning stopper åbenlyst umulige hastigheder, men er ikke avanceret bot-detektion.
- 100 spillere er konfigurationsmålet; der er ikke udført en fuld 100-telefoners belastningstest eller fysisk mobiltest.
- Ved timeout i en duel vinder den, der har valgt, over en manglende besvarelse. Ved afbrudte almindelige spil beholdes den allerede opnåede score eller registreres manglende svar.
- Haptik, fuld skærm og udklipsholder afhænger af browseren; de er ikke nødvendige for at spille.

Tredjepartslicenser findes i [docs/THIRD-PARTY.md](docs/THIRD-PARTY.md).
