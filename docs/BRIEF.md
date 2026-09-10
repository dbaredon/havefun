Build a complete MVP web application for a live multiplayer party mini-game platform intended for birthdays, parties, pre-drinks, and social events.

Promt er på engelsk, men indholdet på hjemmesiden og telefonen skal være på dansk. 

The core concept:

* One host opens the website on a computer connected to a TV or large screen.
* The host creates a game room.
* The TV/host screen displays a large QR code and a short room code.
* Players scan the QR code with their phones.
* Players enter a display name and join the room directly in the browser.
* No app installation and no player accounts are required.
* The phones act as controllers.
* The TV is the shared game screen.
* Everything must update live in real time.

TECH STACK

Use:

* C#
* ASP.NET Core
* .NET 10
* SignalR for real-time communication
* Razor Pages or ASP.NET Core MVC for the web application
* Vanilla JavaScript or lightweight frontend JavaScript
* Modern CSS
* In-memory state for active rooms and games for the MVP
* No database is required initially
* Structure the project so persistence can easily be added later
* Must work well in JetBrains Rider
* Must be deployable to Azure App Service
* Prepare the repository for automatic GitHub deployment

Do not over-engineer the solution.
Prioritize a clean architecture, maintainability, responsive design, and a working multiplayer experience.

PRODUCT EXPERIENCE

The app should feel like a polished party product rather than an admin dashboard.

Visual direction:

* modern
* playful
* festive
* minimalist
* premium
* clean
* mobile-first
* dark mode as the primary visual style
* subtle gradients
* soft glow effects
* rounded cards
* large typography
* smooth animations
* tasteful confetti and celebration effects
* avoid childish/cartoonish design
* avoid excessive visual clutter
* avoid looking like Kahoot

The visual identity should feel suitable for people roughly 18–35 at birthday parties, house parties, pre-drinks and social gatherings.

Use large buttons and very simple interaction patterns because players may be standing, talking, holding drinks, and using the application casually.

RESPONSIVE REQUIREMENTS

The host interface is primarily intended for:

* laptops
* desktops
* TVs
* projectors

The player interface is primarily intended for:

* iPhone
* Android phones

Both must still be responsive.

APPLICATION FLOW

Landing page:

Show a strong hero section with the party game branding.

Primary buttons:

* Create Game
* Join Game

Create Game:

1. Host clicks Create Game.
2. Generate a random short room code such as X7K2.
3. Generate a QR code linking directly to the join URL.
4. Show the lobby on the host screen.
5. Players joining should appear live on the TV.
6. Host can start when ready.

Join Game:

1. Player scans QR code or manually enters room code.
2. Player enters a display name.
3. Player joins room.
4. Player sees a waiting screen.
5. Their name appears instantly on the host screen through SignalR.

LOBBY

Host lobby should show:

* Room code
* Large QR code
* Number of connected players
* Player names
* Start Game button

Example:

ROOM X7K2

[QR CODE]

6 players joined

Tony
Emma
Jonas
Mads
Oliver
Sofie

START GAME

Player lobby should say something like:

You're in 🎉

Waiting for the host to start...

Show the player's name.

GAME SYSTEM

Create a reusable mini-game architecture.

Each mini-game should have a lifecycle similar to:

* Waiting
* Intro
* Countdown
* Playing
* Finished
* Results

Create reusable abstractions/interfaces for mini-games instead of putting all game logic into SignalR hubs or controllers.

For example, something conceptually similar to:

IMiniGame

* StartAsync
* HandlePlayerInputAsync
* FinishAsync
* GetResults

Create a central GameRoom / GameSession system responsible for:

* room ID
* players
* current game
* scores/results
* player connections
* game state
* transitions between games

The server must be authoritative.

Do not trust results calculated by the browser when important game logic can be calculated on the server.

SignalR should be used to:

* join rooms
* update player lists
* start games
* send game state
* receive player actions
* update host screen
* show results
* reconnect players where practical

INITIAL MINI-GAMES

Implement these MVP mini-games.

1. COOKIE CLICKER

All players participate.

Host screen:

* game title
* countdown
* timer
* live leaderboard
* animated player rankings

Player phone:

* very large cookie/button
* tap as quickly as possible
* show personal click count
* haptic feedback if supported by the browser

Default round duration:
10 seconds

When the game ends:

* show full ranking
* highlight top players
* highlight bottom 3

Make round duration configurable.

Do not make the browser responsible for the final score.
The server should validate/count player input appropriately.

Prevent obviously impossible spam rates where reasonable without harming normal gameplay.

2. PERFECT TIMING

All players participate.

Objective:
Stop as close as possible to a target duration.

Example target:
5.000 seconds.

Host screen:

* explain target
* countdown
* GO
* do not reveal each player's running timer
* after everyone finishes, reveal results one by one or with a short animation

Player phone:

* Start button
* after pressing Start, replace it with Stop
* do not display the elapsed time while playing
* player estimates the duration mentally
* when they press Stop, submit timing result

Server should calculate or validate timing as much as reasonably possible.

Results should show:

Emma — 5.003s — difference 0.003s
Tony — 4.961s — difference 0.039s
Jonas — 5.122s — difference 0.122s

Closest to target wins.

Allow target duration to be randomly generated from sensible values such as:
3 seconds
5 seconds
7 seconds
10 seconds

3. REACTION

All players participate.

Host screen:

* READY...
* WAIT...
* after a randomized delay, show GO

Player screen:

* one large full-screen interaction area
* player must tap after GO

If player taps before GO:

* false start
* mark the attempt accordingly

Measure reaction time.

Show leaderboard after the round.

4. QUICK MATH

Generate math problems dynamically in C#.

Do not store a fixed quiz/question list.

Examples:
7 + 13
18 - 7
6 × 4

Adjust difficulty so calculations are suitable for fast party gameplay.

Host screen:

* large generated calculation
* optionally show how many players have answered, but not answers

Player screen:

* numeric input
* Submit button

Track:

* correct/incorrect
* response time

At the end:

* wrong answers rank below correct answers
* correct players rank by response time
* highlight the slowest/wrong players

Generate each math problem and correct answer server-side.

5. DUEL

Randomly select two active players.

Host screen:

TONY
VS
JONAS

Players not selected should see:
"Watch the duel on the big screen."

Selected players see privately on their phone:

* Rock
* Paper
* Scissors

Choices must remain secret until both players have selected.

After both choices:

* reveal both choices on the host screen
* animate the winner
* show winner and loser

Handle ties by replaying the duel.

6. SPIN THE WHEEL

Host screen displays a visual spinning wheel containing all active players.

The server chooses the result securely/randomly.

The animation should land visually on the server-selected player.

Highlight the selected player dramatically.

Player phones can simply show:
"The wheel is spinning..."

This is more of an event than a skill game.

7. HOT POTATO / BOMB

Create a basic multiplayer version.

One random player starts with a bomb.

Their phone clearly shows:
"YOU HAVE THE BOMB"

They must choose another active player to pass it to.

Other players see whether they are safe.

The bomb has a randomized hidden fuse duration.

The server controls the fuse.

When the bomb explodes:

* show dramatic host-screen animation
* reveal who was holding it
* show results

Prevent passing the bomb back instantly to the same player if needed for game balance.

GAME ROTATION

After the lobby, the host should have two modes:

QUICK PLAY
Automatically selects random mini-games.

CHOOSE GAME
Host manually selects a game.

For Quick Play:

* avoid playing the same game twice in a row
* display a short animated transition between games

Example:

COOKIE CLICKER
↓
RESULTS
↓
NEXT GAME
↓
PERFECT TIMING
↓
RESULTS
↓
RANDOM EVENT
↓
SPIN THE WHEEL

Do not permanently eliminate players.
Everyone participates again in subsequent games.

PARTY CONSEQUENCES

Do NOT hard-code alcohol consumption into the core game engine.

Instead implement a configurable generic consequence system.

For example:

* Penalty points
* Challenge
* Custom text

The host may optionally configure custom consequence text for a game, such as:
"Bottom 3: 2 sips"

But the game system itself should work perfectly without alcohol-related consequences.

ROOM MANAGEMENT

Rooms should:

* use short unique codes
* automatically disappear after being inactive for a configurable amount of time
* support players leaving
* support player reconnects where practical
* prevent duplicate names or automatically differentiate them
* maintain stable player IDs separate from SignalR connection IDs

Create a browser-side player token/session identifier so refreshing the phone page does not automatically create a completely new player where avoidable.

HOST SECURITY

Generate a separate host token/identifier when creating a room.

A normal player should not be able to call host-only SignalR methods simply by changing frontend JavaScript.

Validate host permissions server-side.

ERROR HANDLING

Handle:

* invalid room code
* room no longer exists
* connection lost
* host disconnect
* game already started
* duplicate submission
* phone refresh
* player disconnect during a game

Show user-friendly messages rather than raw errors.

SIGNALR DESIGN

Keep SignalR hubs thin.

Do not put the entire application logic inside the Hub.

Hub methods should delegate to services such as:

* RoomService
* GameManager
* PlayerService
* MiniGame implementations

Use SignalR groups for each room.

Separate messages intended for:

* entire room
* host
* individual player

PERFORMANCE

Design the MVP for approximately 5–100 players in a room.

For games such as Cookie Clicker, avoid broadcasting every individual click to every client if this creates unnecessary traffic.

Instead:

* record input efficiently
* broadcast leaderboard/state snapshots at a sensible interval such as several times per second

The app should still feel live.

PROJECT STRUCTURE

Create a clean folder structure similar to:

/Controllers or /Pages
/Hubs
/Services
/Models
/Games
/CookieClicker
/PerfectTiming
/Reaction
/QuickMath
/Duel
/SpinWheel
/HotPotato
/wwwroot
/css
/js

Use dependency injection.

Use strongly typed models where practical.

Avoid unnecessary third-party packages.

QR CODE

Generate a QR code for the join URL.

The join URL should contain the room code automatically.

Example:

https://domain.com/join/X7K2

DESIGN DETAILS

HOST / TV:

Use:

* very large typography
* clear hierarchy
* centered layouts
* animated transitions
* real-time leaderboard animation
* celebration/confetti for winners
* full-screen mode friendly layout

PLAYER PHONE:

Interaction areas should be large.

Avoid tiny controls.

Keep the main action reachable with one thumb.

Use CSS such as:

* min-height: 100dvh
* proper safe-area support
* responsive typography
* prevent accidental zoom where appropriate
* touch-action where appropriate

Use mobile-friendly input types.

Add subtle vibration using navigator.vibrate() where supported for:

* countdown
* successful taps
* receiving the bomb
* game start

Do not make vibration essential to gameplay.

LANDING PAGE COPY

Create suitable placeholder branding and copy for now.

Example concept:

"Turn every phone into a party controller."

"Scan. Join. Play."

Primary CTA:
START A PARTY

Secondary:
JOIN GAME

Do not call the product "PartyGame" everywhere.
Create a temporary brand name that can easily be changed later.

DEVELOPMENT EXPERIENCE

Include:

* README.md
* setup instructions
* run instructions
* architecture overview
* explanation of SignalR flow
* explanation of how to add a new mini-game
* Azure deployment instructions
* GitHub deployment guidance
* useful comments, but avoid over-commenting obvious code

The solution must run locally with:

dotnet restore
dotnet run

When running locally, show the local URL clearly.

Also configure development so another phone on the same LAN can access the application when the developer intentionally binds ASP.NET Core to the machine's LAN interface.

However, production architecture should assume normal internet hosting.

TESTING

Add meaningful automated tests for core game logic.

At minimum test:

* Rock Paper Scissors winner calculation
* Quick Math answer evaluation
* Perfect Timing ranking
* Reaction false start handling
* room creation
* player joining
* duplicate submissions where relevant

IMPORTANT IMPLEMENTATION RULES

* Build working functionality, not mock screens.
* Avoid TODO placeholders for core functionality.
* Avoid fake multiplayer.
* SignalR should actually connect host and players.
* Multiple browser tabs should be usable to test multiple players locally.
* Prioritize reliability over unnecessary abstractions.
* Keep game logic server authoritative.
* Keep UI polished.
* Make the MVP usable immediately after running the project.

Before finishing:

1. Build the project.
2. Fix compilation errors.
3. Run the automated tests.
4. Fix failing tests.
5. Verify the main create-room → QR/join → lobby → game → results flow.
6. Provide a concise final summary of what was created and any remaining non-critical limitations.

