Key Components and Functionality
1. Fields and Data Structures
Arena Management: 
A List<Arena> stores arena data (Name, RingCenter, ActivationSpot), loaded from FightClubArenas.ini. If the file is missing, it creates a default arena. This is a solid approach for flexibility and modularity.

Example default: Underground Fight Club at coordinates (-147.3509, 1986.372, 10.7097).

Fighters and Crowds: 
List<Fighter> for participants and List<Ped> for crowd members. The Fighter class tracks Ped, Gang, Nickname, Wins, and Losses.

Individual references (currentFighter, challenger) and outcome tracking (winnerFighter, loserFighter) are well-defined.

Gangs and Weapons: 
A Dictionary<string, PedHash[]> maps gang names to ped models (e.g., "The Ballers" to BallaEast01GMY, etc.), enabling varied fighter spawning.

An array of melee weapon hashes (e.g., bat, crowbar) adds variety to fights.

Player Stats and Weapons: 
Tracks playerWins, playerLosses, totalEarnings, and playerWinStreak.

List<PlayerWeaponData> stores the player’s weapons (hash, ammo, components) before fights, ensuring they’re restored afterward.

State Flags and Timers: 
Flags like bettingActive, playerFighting, choosingMode, and promptingContinue manage the fight club’s state.

Timers (bettingWindowTimer, promptDuration, etc.) control phases like betting and post-fight prompts.

2. Core Methods
LoadArenasFromIni(): 
Reads arena data from an INI file with a fallback to a default arena if the file is missing or malformed. The parsing logic is robust, handling sections and key-value pairs correctly.



GenerateNickname(): 
Creates random nicknames (e.g., "Mad Dog", "Fierce Viper") based on ped gender. It uses specific PedHash checks for female models, which works but could be more generalized (e.g., using a game API for gender detection if available).



OnKeyDown(): 
Handles player inputs:
Left/Right: Bet or fight during the choice window.

Enter/0-9/Backspace: Enter custom bet amounts.

Y/U/N: Continue or stop after a player fight.

F5: End the fight club.

Logic is clear and respects state conditions (e.g., bettingActive, choosingMode).



JoinFightAsPlayer(): 
Sets up the player as a fighter, removes weapons, positions them in the ring, and enforces a 3-second delay with movement/attack controls disabled. Camera controls remain 
enabled, which is thoughtful for user experience.



ContinueFighting(): 
Allows the player to keep fighting after a win, with an optional heal for $250. Boss fights trigger after a bossFightThreshold (5 wins), resetting the streak. The method handles edge cases like insufficient funds.



StopFighting(): 
Ends the player’s fighting session, restores weapons, and sets up a new AI fight round. It cleans up properly and provides feedback via notifications.



StorePlayerWeapons() and RestorePlayerWeapons(): 
These methods meticulously save and restore weapon hashes, ammo, ammo types (including special types like FMJ), and components. This ensures the player’s inventory remains intact.



StartFightClub(): 
Initializes an arena, spawns fighters and a crowd, and starts the betting phase. It includes error handling for missing arenas or fighter spawn failures.



BeginFight(): 
Starts combat between fighters, resetting their state if they’re not the player. It checks for fighter validity to prevent crashes.



CreateFighter(): 
Spawns a ped with gang affiliation, nickname, and health based on role (boss: 300+, leader: 200+, regular: 100+). Bosses get armor and higher stats, and some fighters receive random melee weapons. The streak bonus (playerWinStreak * 20) makes fights harder over time—a great progression mechanic.



CalculateOdds(): 
Computes betting odds based on fighter health (health / 30, capped 1-10). Simple but effective for a betting system.



BetOnFighter(): 
Deducts the bet amount and assigns the chosen fighter, with checks for sufficient funds and valid state.



SpawnCrowd(): 
Places 6 spectators around the ring, facing inward. The math for positioning and heading is correct, enhancing immersion.



OnTick(): 
The main loop:
Inactive State: Checks for player proximity to activation spots and starts the fight club.

Active State: Manages betting timers, displays fighter markers, and checks for fight endings (dead/unconscious fighters).

Player Death: Custom respawn logic moves the player to the ring’s edge, avoiding hospital respawns. This is complex but works to keep the player in the fight club area.

Subtitles display fighter info and player stats, improving usability.



EndFight(): 
Determines the winner/loser, updates records, awards winnings (or penalties for player loss), and prompts the player to continue if they won. It transitions smoothly to the next round via DelayTimer().



HandlePlayerDeath(): 
Restores weapons, applies a $500 penalty (or bankrupts the player), and sets up a new round, preserving the AI winner if alive.



DelayTimer(): 
Waits 10 seconds, cleaning up the loser after 8 seconds, then sets up the next round with the winner (if alive) or new fighters.



EndFightClub(): 
Fully cleans up fighters, crowd, and state, restores weapons, and provides a stats summary.

