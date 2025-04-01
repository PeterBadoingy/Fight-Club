using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using GTA.Math;
using GTA.UI;
using System.IO;
using System.Linq;

public class FightClub : Script
{
    private Vector3 ringCenter;
    private Vector3 activationSpot;
    private string currentArenaName;
    private int baseBetAmount = 100;
    private int promptDuration = 15000;
    private int choiceWindowTimer = 15000;
    private int bettingWindowTimer = 15000;
    private int bossFightThreshold = 5;
    private int healCost = 250;

    private List<Fighter> fightParticipants = new List<Fighter>();
    private List<Ped> crowd = new List<Ped>();
    private Fighter currentFighter;
    private Fighter challenger;
    private bool bettingActive = false;
    private bool playerFighting = false;
    private bool choosingMode = true;
    private bool promptingContinue = false;
    private Fighter bettedFighter = null;
    private int betAmount;
    private Random rand = new Random();
    private Dictionary<string, PedHash[]> gangPeds = new Dictionary<string, PedHash[]>
    {
        { "The Ballers", new PedHash[] { PedHash.BallaEast01GMY, PedHash.BallaOrig01GMY, PedHash.Ballas01GFY, PedHash.Ballasog, PedHash.BallasLeader, PedHash.BallaSout01GMY } },
        { "The Families", new PedHash[] { PedHash.Families01GFY, PedHash.Famfor01GMY, PedHash.Famdnf01GMY, PedHash.Famca01GMY, PedHash.Famdd01 } },
        { "The Lost MC", new PedHash[] { PedHash.Lost01GMY, PedHash.Lost01GFY, PedHash.Lost02GMY, PedHash.Lost03GMY } },
        { "The Vagos", new PedHash[] { PedHash.Vagos01GFY, PedHash.VagosFun01, PedHash.VagosLeader, PedHash.VagosSpeak } },
        { "The Armenian Mob", new PedHash[] { PedHash.ArmGoon01GMM, PedHash.ArmGoon02GMY, PedHash.ArmBoss01GMM, PedHash.ArmLieut01GMM } },
        { "The Triads", new PedHash[] { PedHash.ChiGoon01GMM, PedHash.ChiGoon02GMM, PedHash.ChiCold01GMM } },
        { "The Korean Mob", new PedHash[] { PedHash.Korean01GMY, PedHash.Korean02GMY, PedHash.KorLieut01GMY, PedHash.KorBoss01GMM } },
        { "The Salvadorans", new PedHash[] { PedHash.SalvaBoss01GMY, PedHash.SalvaGoon01GMY, PedHash.SalvaGoon02GMY, PedHash.SalvaGoon03GMY } },
        { "The Mexican Cartel", new PedHash[] { PedHash.MexGoon01GMY, PedHash.MexGoon03GMY, PedHash.MexBoss01GMM, PedHash.MexBoss02GMM } },
        { "Unaffiliated", new PedHash[] { PedHash.BankRobber01AMM } }
    };
    private PedHash bossPed = PedHash.Bodybuild01AFM;
    private uint[] meleeWeapons = 
    {
        0x958A4A8F, // Bat
        0xF9E6AA4B, // Bottle
        0x84BD7BFD, // Crowbar
        0x440E4788, // Golf Club
        0x4E875F73, // Hammer
        0xD8DF3C3C, // Knuckle Duster
        0xDFE37640, // Switchblade
        0x678B81B1, // Nightstick
        0x19044EE0, // Wrench
        0x94117305, // Pool Cue
    };

    private Fighter winnerFighter = null;
    private Fighter loserFighter = null;
    private int bettingWindowStartTime = 0;
    private int promptStartTime = 0;
    private bool enteringBet = false;

    private string typedBet = "";

    private int playerWins = 0;
    private int playerLosses = 0;
    private int totalEarnings = 0;
    private int playerWinStreak = 0;
    private int[] fighterOdds = new int[2];

    private List<PlayerWeaponData> playerWeapons = new List<PlayerWeaponData>();
    private bool isFightClubActive = false;

    private List<Arena> arenas = new List<Arena>();

    private class Arena
    {
        public string Name { get; set; }
        public Vector3 RingCenter { get; set; }
        public Vector3 ActivationSpot { get; set; }

        public Arena(string name, Vector3 ringCenter, Vector3 activationSpot)
        {
            Name = name;
            RingCenter = ringCenter;
            ActivationSpot = activationSpot;
        }
    }

    private class Fighter
    {
        public Ped Ped { get; set; }
        public string Gang { get; set; }
        public string Nickname { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }

        public Fighter(Ped ped, string gang, string nickname)
        {
            Ped = ped;
            Gang = gang;
            Nickname = nickname;
            Wins = 0;
            Losses = 0;
        }
    }

    private struct PlayerWeaponData
    {
        public uint Hash;
        public int TotalAmmo;
        public uint AmmoType;
        public List<WeaponComponentHash> Components;

        public PlayerWeaponData(WeaponHash hash, int totalAmmo, uint ammoType, List<WeaponComponentHash> components)
        {
            Hash = (uint)hash;
            TotalAmmo = totalAmmo;
            AmmoType = ammoType;
            Components = components;
        }
    }

    private readonly uint[] specialAmmoTypes = new uint[]
    {
        0xF3956D61, // FMJ
        0x85FEA109, // Incendiary
        0x4C24806E, // Hollow Point
        0x8FBA139B  // Tracer
    };

    public FightClub()
    {
        Tick += OnTick;
        KeyDown += OnKeyDown;
        betAmount = baseBetAmount;
        LoadArenasFromIni();
    }

    private void LoadArenasFromIni()
    {
        string iniPath = Path.Combine("scripts", "FightClubArenas.ini");
        if (!File.Exists(iniPath))
        {
            string defaultIni = "[Underground Fight Club]\nRingCenter=-147.3509,1986.372,10.7097\nActivationSpot=-150.9064,1980.082,10.70783\n";
            File.WriteAllText(iniPath, defaultIni);
        }

        string[] lines = File.ReadAllLines(iniPath);
        string currentName = null;
        Vector3 ringCenter = Vector3.Zero;
        Vector3 activationSpot = Vector3.Zero;

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                if (currentName != null && ringCenter != Vector3.Zero && activationSpot != Vector3.Zero)
                {
                    arenas.Add(new Arena(currentName, ringCenter, activationSpot));
                }
                currentName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                ringCenter = Vector3.Zero;
                activationSpot = Vector3.Zero;
            }
            else if (trimmedLine.Contains("="))
            {
                string[] parts = trimmedLine.Split('=');
                string key = parts[0].Trim();
                string[] coords = parts[1].Trim().Split(',');
                if (coords.Length == 3)
                {
                    Vector3 vector = new Vector3(
                        float.Parse(coords[0].Trim()),
                        float.Parse(coords[1].Trim()),
                        float.Parse(coords[2].Trim())
                    );
                    if (key == "RingCenter") ringCenter = vector;
                    else if (key == "ActivationSpot") activationSpot = vector;
                }
            }
        }
        if (currentName != null && ringCenter != Vector3.Zero && activationSpot != Vector3.Zero)
        {
            arenas.Add(new Arena(currentName, ringCenter, activationSpot));
        }

        if (arenas.Count == 0)
        {
            arenas.Add(new Arena("Underground Fight Club", new Vector3(-147.3509f, 1986.372f, 10.7097f), new Vector3(-150.9064f, 1980.082f, 10.70783f)));
        }
    }

    private string GenerateNickname(PedHash pedModel)
    {
        string[] malePrefixes = { "Mad", "Iron", "Sly", "Bloody", "Quick", "Razor", "Silent", "Big", "Grim", "Sick", "Wild", "Cold", "Hard", "Lone", "Rough", "Steel" };
        string[] femalePrefixes = { "Fierce", "Vicious", "Swift", "Deadly", "Savage", "Sharp", "Quiet", "Tall", "Cruel", "Hot", "Bold", "Dark", "Tough", "Sly", "Wicked", "Frost" };
        string[] suffixes = { "Dog", "Blade", "Fist", "Shadow", "Bull", "Viper", "Hawk", "Wolf", "Skull", "Claw", "Reaper", "Ghost", "Tiger", "Raven", "Cobra", "Shark", "Bruiser", "Ace" };
        bool isFemale = pedModel == PedHash.Ballas01GFY || pedModel == PedHash.Lost01GFY || 
                        pedModel == PedHash.Vagos01GFY || pedModel == PedHash.Families01GFY;
        string[] prefixes = isFemale ? femalePrefixes : malePrefixes;
        return prefixes[rand.Next(prefixes.Length)] + " " + suffixes[rand.Next(suffixes.Length)];
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (bettingActive && choosingMode && !playerFighting && !enteringBet && bettedFighter == null && Game.GameTime - bettingWindowStartTime < choiceWindowTimer)
        {
            if (e.KeyCode == Keys.Left)
            {
                choosingMode = false;
                Notification.PostTicker(string.Format("You chose to bet. Bet on Red (Left, ←, {0}:1) or Blue (Right, →, {1}:1). Current bet: ${2}", fighterOdds[0], fighterOdds[1], betAmount), true, true);
            }
            else if (e.KeyCode == Keys.Right)
            {
                JoinFightAsPlayer();
                return;
            }
            return;
        }

        if (bettingActive && !playerFighting && !choosingMode)
        {
            if (e.KeyCode == Keys.Left && !enteringBet && bettedFighter == null && fightParticipants.Count > 0)
            {
                BetOnFighter(fightParticipants[0]);
            }
            else if (e.KeyCode == Keys.Right && !enteringBet && bettedFighter == null && fightParticipants.Count > 1)
            {
                BetOnFighter(fightParticipants[1]);
            }
            else if (e.KeyCode == Keys.Enter && bettedFighter == null)
            {
                if (!enteringBet)
                {
                    enteringBet = true;
                    typedBet = "";
                    Notification.PostTicker("Type your bet amount (0-9), Backspace to correct, Enter to confirm.", true, true);
                }
                else
                {
                    ConfirmBetAmount();
                }
            }
            else if (enteringBet)
            {
                if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9 && typedBet.Length < 5)
                {
                    typedBet += (e.KeyCode - Keys.D0).ToString();
                    Notification.PostTicker("Current bet: $" + typedBet, true, true);
                }
                else if (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9 && typedBet.Length < 5)
                {
                    typedBet += (e.KeyCode - Keys.NumPad0).ToString();
                    Notification.PostTicker("Current bet: $" + typedBet, true, true);
                }
                else if (e.KeyCode == Keys.Back && typedBet.Length > 0)
                {
                    typedBet = typedBet.Substring(0, typedBet.Length - 1);
                    Notification.PostTicker("Current bet: $" + (typedBet.Length > 0 ? typedBet : "0"), true, true);
                }
            }
        }

        if (promptingContinue)
        {
            if (e.KeyCode == Keys.Y)
            {
                ContinueFighting(true);
                promptingContinue = false;
            }
            else if (e.KeyCode == Keys.U)
            {
                ContinueFighting(false);
                promptingContinue = false;
            }
            else if (e.KeyCode == Keys.N)
            {
                StopFighting();
                promptingContinue = false;
            }
        }

        // Modified: F5 now triggers manual deactivation
        if (e.KeyCode == Keys.F5 && isFightClubActive)
        {
            EndFightClub(true); // Manual deactivation via F5
        }
    }

    private void JoinFightAsPlayer()
    {
        playerFighting = true;
        bettingActive = false;
        choosingMode = false;
        Notification.PostTicker("You’ve joined the fight! Round starts in 3 seconds. (Weapons removed)", true, true);

        if (challenger != null && challenger.Ped.Exists())
        {
            challenger.Ped.Delete();
        }
        challenger = new Fighter(Game.Player.Character, "Player", "The Champ");
        fightParticipants[1] = challenger;
        challenger.Ped.Position = ringCenter + new Vector3(1.0f, 0, 0);
        challenger.Ped.Heading = 90f;

        Game.Player.Character.Health = Game.Player.Character.MaxHealth;
        Game.Player.Character.Weapons.RemoveAll();

        Function.Call(Hash.CLEAR_PED_TASKS, Game.Player.Character);

        int delayStart = Game.GameTime;
        while (Game.GameTime - delayStart < 3000)
        {
            Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, 32, true); // MoveUpOnly (W)
            Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, 33, true); // MoveDownOnly (S)
            Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, 34, true); // MoveLeftOnly (A)
            Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, 35, true); // MoveRightOnly (D)
            Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, 24, true); // Attack (Left Click)
            Function.Call(Hash.ENABLE_CONTROL_ACTION, 0, 1, true);  // LookLeftOnly
            Function.Call(Hash.ENABLE_CONTROL_ACTION, 0, 2, true);  // LookRightOnly
            Function.Call(Hash.ENABLE_CONTROL_ACTION, 0, 3, true);  // LookUpOnly
            Function.Call(Hash.ENABLE_CONTROL_ACTION, 0, 4, true);  // LookDownOnly
            Script.Yield();
        }

        BeginFight();
    }

    private void ContinueFighting(bool heal)
    {
        bool isBossFight = playerWinStreak >= bossFightThreshold - 1;
        playerFighting = true;
        playerWinStreak++;

        if (loserFighter != null && loserFighter.Ped.Exists() && loserFighter.Ped != Game.Player.Character)
        {
            loserFighter.Ped.Delete();
            loserFighter = null;
        }

        Game.Player.Character.Position = ringCenter + new Vector3(-1.0f, 0, 0);
        Game.Player.Character.Heading = 270f;

        if (heal)
        {
            if (Game.Player.Money >= healCost)
            {
                Game.Player.Money -= healCost;
                totalEarnings -= healCost;
                Game.Player.Character.Health = Game.Player.Character.MaxHealth;
                Notification.PostTicker(string.Format("You paid ${0} to heal! Round starts in 3 seconds. Streak: {1}", healCost, playerWinStreak), true, true);
            }
            else
            {
                Notification.PostTicker(string.Format("Not enough money (${0}) to heal! Round starts in 3 seconds. Streak: {1}", healCost, playerWinStreak), true, true);
            }
        }
        else
        {
            Notification.PostTicker(string.Format("Continuing as-is! Round starts in 3 seconds. Streak: {0}", playerWinStreak), true, true);
        }

        currentFighter = new Fighter(Game.Player.Character, "Player", "The Champ");
        Game.Player.Character.Weapons.RemoveAll();
        challenger = CreateFighter(true, 1, isBossFight);

        if (challenger != null)
        {
            fightParticipants.Clear();
            fightParticipants.Add(currentFighter);
            fightParticipants.Add(challenger);

            Function.Call(Hash.CLEAR_PED_TASKS, Game.Player.Character);

            int delayStart = Game.GameTime;
            while (Game.GameTime - delayStart < 3000)
            {
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, 32, true); // MoveUpOnly (W)
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, 33, true); // MoveDownOnly (S)
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, 34, true); // MoveLeftOnly (A)
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, 35, true); // MoveRightOnly (D)
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, 24, true); // Attack (Left Click)
                Function.Call(Hash.ENABLE_CONTROL_ACTION, 0, 1, true);  // LookLeftOnly
                Function.Call(Hash.ENABLE_CONTROL_ACTION, 0, 2, true);  // LookRightOnly
                Function.Call(Hash.ENABLE_CONTROL_ACTION, 0, 3, true);  // LookUpOnly
                Function.Call(Hash.ENABLE_CONTROL_ACTION, 0, 4, true);  // LookDownOnly
                Script.Yield();
            }

            BeginFight();
        }
        else
        {
            Notification.PostTicker("Failed to spawn challenger. Ending fight...", true, true);
            EndFightClub(false);
        }
    }

    private void StopFighting()
    {
        Notification.PostTicker(string.Format("You chose to stop fighting. Stats: Wins: {0}, Losses: {1}, Earnings: ${2}. Preparing new fighters...", playerWins, playerLosses, totalEarnings), true, true);
        playerFighting = false;

        foreach (Fighter f in fightParticipants)
        {
            if (f != null && f.Ped.Exists() && f.Ped != Game.Player.Character)
            {
                f.Ped.Delete();
            }
        }
        fightParticipants.Clear();
        if (loserFighter != null && loserFighter.Ped.Exists() && loserFighter.Ped != Game.Player.Character)
        {
            loserFighter.Ped.Delete();
            loserFighter = null;
        }
        currentFighter = null;
        challenger = null;
        winnerFighter = null;
        bettedFighter = null;

        Game.Player.Character.Health = Game.Player.Character.MaxHealth;
        Game.Player.Character.Position = ringCenter + new Vector3(0, 5.0f, 0);
        Game.Player.Character.Heading = 0f;
        RestorePlayerWeapons();

        currentFighter = CreateFighter(true, -1);
        challenger = CreateFighter(true, 1);
        if (currentFighter != null && challenger != null)
        {
            fightParticipants.Add(currentFighter);
            fightParticipants.Add(challenger);
            fighterOdds[0] = CalculateOdds(currentFighter.Ped.Health);
            fighterOdds[1] = CalculateOdds(challenger.Ped.Health);
            bettingActive = true;
            choosingMode = true;
            bettingWindowStartTime = Game.GameTime;
            Notification.PostTicker("New round! Press Left (←) to bet, Right (→) to fight (15s to choose).", true, true);
        }
        else
        {
            Notification.PostTicker("Failed to spawn fighters. Ending Fight Club.", true, true);
            isFightClubActive = false;
        }
    }

    private void StorePlayerWeapons()
    {
        playerWeapons.Clear();
        foreach (Weapon weapon in Game.Player.Character.Weapons)
        {
            if (weapon.Hash != WeaponHash.Unarmed)
            {
                int totalAmmo = Function.Call<int>(Hash.GET_AMMO_IN_PED_WEAPON, Game.Player.Character, (int)weapon.Hash);
                uint ammoType = (uint)Function.Call<int>(Hash.GET_PED_AMMO_TYPE_FROM_WEAPON, Game.Player.Character, (int)weapon.Hash);
                List<WeaponComponentHash> components = new List<WeaponComponentHash>();
                foreach (WeaponComponent component in weapon.Components)
                {
                    if (Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON_COMPONENT, Game.Player.Character, (int)weapon.Hash, (int)component.ComponentHash))
                    {
                        components.Add(component.ComponentHash);
                    }
                }
                playerWeapons.Add(new PlayerWeaponData(weapon.Hash, totalAmmo, ammoType, components));
            }
        }
    }

    private void RestorePlayerWeapons()
    {
        Game.Player.Character.Weapons.RemoveAll();
        foreach (var weaponData in playerWeapons)
        {
            uint hash = weaponData.Hash;
            int totalAmmo = weaponData.TotalAmmo;
            uint ammoType = weaponData.AmmoType;

            Function.Call(Hash.GIVE_WEAPON_TO_PED, Game.Player.Character, (int)hash, 0, false, true);
            foreach (WeaponComponentHash component in Enum.GetValues(typeof(WeaponComponentHash)))
            {
                if (Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON_COMPONENT, Game.Player.Character, (int)hash, (int)component))
                {
                    Function.Call(Hash.REMOVE_WEAPON_COMPONENT_FROM_PED, Game.Player.Character, (int)hash, (int)component);
                }
            }
            foreach (WeaponComponentHash componentHash in weaponData.Components)
            {
                Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_PED, Game.Player.Character, (int)hash, (int)componentHash);
            }
            if (totalAmmo > 0)
            {
                Function.Call(Hash.SET_PED_AMMO, Game.Player.Character, (int)hash, 0);
                Function.Call(Hash.ADD_AMMO_TO_PED, Game.Player.Character, (int)hash, totalAmmo);
                if (Array.Exists(specialAmmoTypes, t => t == ammoType))
                {
                    Function.Call(Hash.SET_PED_AMMO_BY_TYPE, Game.Player.Character, (int)ammoType, totalAmmo);
                }
            }
        }
    }

    private void ConfirmBetAmount()
    {
        enteringBet = false;
        int newBetAmount;
        if (int.TryParse(typedBet, out newBetAmount))
        {
            int maxBet = Math.Min(1000, Game.Player.Money);
            if (newBetAmount >= 10 && newBetAmount <= maxBet)
            {
                betAmount = newBetAmount;
                Notification.PostTicker("Bet amount set to $" + betAmount, true, true);
            }
            else if (newBetAmount < 10)
            {
                betAmount = 10;
                Notification.PostTicker("Minimum bet is $10. Set to $10.", true, true);
            }
            else
            {
                betAmount = maxBet;
                Notification.PostTicker("Max bet limited to $" + maxBet, true, true);
            }
        }
        else
        {
            Notification.PostTicker("Invalid bet amount. Keeping previous bet.", true, true);
        }
        typedBet = "";
    }

    private void StartFightClub(string arenaName)
    {
        ClearFighters();
        var selectedArena = arenas.Find(a => a.Name == arenaName);
        if (selectedArena == null)
        {
            Notification.PostTicker("Arena not found. Using default.", true, true);
            selectedArena = arenas[0];
        }
        ringCenter = selectedArena.RingCenter;
        activationSpot = selectedArena.ActivationSpot;
        currentArenaName = selectedArena.Name;
        Notification.PostTicker("Fight Club started at " + selectedArena.Name + "!", true, true);

        bettingActive = true;
        playerFighting = false;
        choosingMode = true;
        promptingContinue = false;
        bettingWindowStartTime = Game.GameTime;
        StorePlayerWeapons();

        currentFighter = CreateFighter(true, -1);
        challenger = CreateFighter(true, 1);

        if (currentFighter == null || challenger == null)
        {
            Notification.PostTicker("Failed to spawn fighters. Try again.", true, true);
            ClearFighters();
            return;
        }

        fightParticipants.Clear();
        fightParticipants.Add(currentFighter);
        fightParticipants.Add(challenger);

        fighterOdds[0] = CalculateOdds(currentFighter.Ped.Health);
        fighterOdds[1] = CalculateOdds(challenger.Ped.Health);

        SpawnCrowd();

        Notification.PostTicker("New round! Press Left (←) to bet, Right (→) to fight (15s to choose).", true, true);
    }

    private void BeginFight()
    {
        if (currentFighter == null || !currentFighter.Ped.Exists() || challenger == null || !challenger.Ped.Exists())
        {
            Notification.PostTicker("Cannot start fight: Fighters missing.", true, true);
            ClearFighters();
            return;
        }

        if (!playerFighting || currentFighter.Ped != Game.Player.Character)
            ResetFighter(currentFighter.Ped);
        if (!playerFighting || challenger.Ped != Game.Player.Character)
            ResetFighter(challenger.Ped);

        currentFighter.Ped.Task.Combat(challenger.Ped);
        challenger.Ped.Task.Combat(currentFighter.Ped);
    }

    private Fighter CreateFighter(bool centerSpawn = false, int side = 0, bool isBoss = false)
    {
        Vector3 spawnOffset = centerSpawn ? new Vector3(side * 1.0f, 0, 0) : new Vector3(rand.Next(0, 2) * 2 - 1, rand.Next(1, 3), 0);
        Vector3 spawnPos = ringCenter + spawnOffset;

        string gang = isBoss ? "Boss" : gangPeds.Keys.ElementAt(rand.Next(gangPeds.Count));
        PedHash pedModel = isBoss ? bossPed : gangPeds[gang][rand.Next(gangPeds[gang].Length)];
        Ped ped = World.CreatePed(pedModel, spawnPos);

        if (ped != null && ped.Exists())
        {
            bool isLeader = pedModel == PedHash.BallasLeader || pedModel == PedHash.VagosLeader || pedModel == PedHash.VagosSpeak || 
                           pedModel == PedHash.ArmBoss01GMM || pedModel == PedHash.ArmLieut01GMM || pedModel == PedHash.KorBoss01GMM || 
                           pedModel == PedHash.SalvaBoss01GMY || pedModel == PedHash.MexBoss01GMM || pedModel == PedHash.MexBoss02GMM;
            Fighter fighter = new Fighter(ped, gang, GenerateNickname(pedModel));
            ped.Weapons.RemoveAll();
            int baseHealth = isBoss ? 300 : (isLeader ? 200 : 100);
            int randomHealthBonus = rand.Next(0, 151);
            int streakBonus = playerWinStreak * 20;
            ped.Health = baseHealth + randomHealthBonus + (isBoss ? 100 + streakBonus : streakBonus);
            ped.MaxHealth = ped.Health;

            bool giveWeapon = isBoss || rand.NextDouble() < 0.25;
            if (giveWeapon)
            {
                uint weapon = meleeWeapons[rand.Next(meleeWeapons.Length)];
                Function.Call(Hash.GIVE_WEAPON_TO_PED, ped, weapon, 1, false, true);
                string message = isBoss ? "The boss " + fighter.Nickname + " wields a deadly weapon!" :
                                (isLeader ? fighter.Nickname + " from " + gang + " steps up with a weapon!" :
                                fighter.Nickname + " from " + gang + " has a melee weapon!");
                Notification.PostTicker(message, true, true);
            }

            if (isBoss)
            {
                ped.Armor = 100;
                ped.FiringPattern = FiringPattern.FullAuto;
                ped.CanSufferCriticalHits = false;
                Notification.PostTicker("A tough, armored boss, " + fighter.Nickname + ", approaches!", true, true);
            }

            if (centerSpawn)
            {
                ped.Heading = side == -1 ? 270f : 90f;
            }
            return fighter;
        }
        return null;
    }

    private int CalculateOdds(int health)
    {
        return Math.Max(1, Math.Min(10, (int)Math.Round((float)health / 30)));
    }

    private void ResetFighter(Ped fighter)
    {
        if (fighter != null && fighter.Exists())
        {
            fighter.Health = fighter.MaxHealth;
            fighter.IsInvincible = false;
            fighter.Task.ClearAll();
            fighter.Task.StandStill(-1);
        }
    }

    private void BetOnFighter(Fighter selectedFighter)
    {
        if (!bettingActive || selectedFighter == null || !selectedFighter.Ped.Exists() || bettedFighter != null)
        {
            Notification.PostTicker("You cannot place a bet now.", true, true);
            return;
        }

        bettedFighter = selectedFighter;
        int odds = GetOdds(fightParticipants.IndexOf(selectedFighter));
        Notification.PostTicker(string.Format("You bet on {0} from {1} ({2}:1)", bettedFighter.Nickname, bettedFighter.Gang, odds), true, true);

        if (Game.Player.Money >= betAmount)
        {
            Game.Player.Money -= betAmount;
            Notification.PostTicker("Bet placed: $" + betAmount, true, true);
        }
        else
        {
            Notification.PostTicker("You don’t have enough money to place this bet.", true, true);
            bettedFighter = null;
        }
    }

    private int GetOdds(int fighterIndex)
    {
        return fighterOdds[fighterIndex];
    }

    private void SpawnCrowd()
    {
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 90f * (float)Math.PI / 270f;
            Vector3 pos = ringCenter + new Vector3((float)Math.Cos(angle) * 5f, (float)Math.Sin(angle) * 5f, 0);
            Ped spectator = World.CreatePed(PedHash.Bouncer01SMM, pos);
            if (spectator != null && spectator.Exists())
            {
                Vector3 direction = ringCenter - spectator.Position;
                direction.Z = 0;
                direction.Normalize();
                float heading = (float)(Math.Atan2(direction.Y, direction.X) * 180 / Math.PI);
                heading = (heading + 90f + 180f) % 360f;
                if (heading < 0) heading += 360f;
                spectator.Heading = heading;
                spectator.Task.StandStill(-1);
                crowd.Add(spectator);
            }
        }
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (!isFightClubActive)
        {
            foreach (var arena in arenas)
            {
                Vector3 markerPos = arena.ActivationSpot + new Vector3(0f, 0f, -0.5f);
                Function.Call(Hash.DRAW_MARKER, 0, markerPos.X, markerPos.Y, markerPos.Z, 0f, 0f, 0f, 0f, 0f, 0f, 0.5f, 0.5f, 0.5f, 255, 255, 0, 100, false, true, 2, false, 0, 0, false);
                if (Vector3.Distance(Game.Player.Character.Position, arena.ActivationSpot) < 5.0f)
                {
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "Move to the yellow marker to start Fight Club at " + arena.Name + ".");
                    Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, -1);
                }
                if (!Game.Player.Character.IsInVehicle() && Vector3.Distance(Game.Player.Character.Position, arena.ActivationSpot) < 2.0f)
                {
                    StartFightClub(arena.Name);
                    isFightClubActive = true;
                    break;
                }
            }
        }

        // Added: Distance-based deactivation (20 units from activationSpot)
        if (isFightClubActive)
        {
            float distance = Vector3.Distance(Game.Player.Character.Position, activationSpot);
            if (distance > 20.0f)
            {
                EndFightClub(false); // Distance-based deactivation
            }
        }

        if (playerFighting)
        {
            Function.Call(Hash.HIDE_HUD_COMPONENT_THIS_FRAME, 14);

            if (Game.Player.Character.IsDead || Game.Player.Character.Health <= 0)
            {
                int currentHour = Function.Call<int>(Hash.GET_CLOCK_HOURS);
                int currentMinute = Function.Call<int>(Hash.GET_CLOCK_MINUTES);
                int currentSecond = Function.Call<int>(Hash.GET_CLOCK_SECONDS);

                Vector3 respawnPos = ringCenter + new Vector3(0, 5.0f, 0);
                Function.Call((Hash)0x706B5EDCAA7FA663, respawnPos.X, respawnPos.Y, respawnPos.Z, 0f); // SET_RESTART_COORD_OVERRIDE

                Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "respawn_controller");
                Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "replay_controller");
                Function.Call((Hash)0x2C2B3493FBF51C71, true); // PAUSE_DEATH_ARREST_RESTART
                Function.Call(Hash.IGNORE_NEXT_RESTART, true);
                Function.Call(Hash.SET_FADE_OUT_AFTER_DEATH, false);
                Function.Call(Hash.SET_FADE_IN_AFTER_DEATH_ARREST, false);
                Function.Call(Hash.SET_FADE_OUT_AFTER_ARREST, false);
                for (int i = 0; i <= 4; i++)
                    Function.Call(Hash.DISABLE_HOSPITAL_RESTART, i, true);
                Function.Call(Hash.DISPLAY_HUD, false);
                Function.Call(Hash.PAUSE_CLOCK, true);
                Function.Call(Hash.DO_SCREEN_FADE_OUT, 0);

                while (!GTA.UI.Screen.IsFadedOut) Script.Wait(0);

                Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "respawn_controller");

                Function.Call(Hash.NETWORK_RESURRECT_LOCAL_PLAYER, respawnPos.X, respawnPos.Y, respawnPos.Z, 0f, false, false);
                Function.Call(Hash.NETWORK_REQUEST_CONTROL_OF_ENTITY, Game.Player.Character);
                Function.Call(Hash.CLEAR_PED_TASKS_IMMEDIATELY, Game.Player.Character);
                Function.Call(Hash.SET_ENTITY_VELOCITY, Game.Player.Character, 0f, 0f, -1f);
                Function.Call(Hash.SET_ENTITY_COLLISION, Game.Player.Character, true, true);
                Game.Player.Character.Health = Game.Player.Character.MaxHealth;
                Game.Player.Character.Position = respawnPos;
                Game.Player.Character.Heading = 0f;
                Function.Call(Hash.SET_ENTITY_INVINCIBLE, Game.Player.Character, true);
                Function.Call(Hash.NETWORK_SET_LOCAL_PLAYER_INVINCIBLE_TIME, 500);

                Function.Call(Hash.CLEAR_PED_BLOOD_DAMAGE, Game.Player.Character);
                Function.Call((Hash)0xC0AA53F866B3134D); // FORCE_GAME_STATE_PLAYING (early)
                Function.Call(Hash.SET_ENTITY_RENDER_SCORCHED, Game.Player.Character, false);
                Function.Call(Hash.SET_PLAYER_CONTROL, Game.Player, false, 0);

                Script.Wait(1000);
                Function.Call(Hash.DO_SCREEN_FADE_IN, 500);
                Script.Wait(250);
                for (int i = 0; i <= 20; i++)
                    Function.Call(Hash.RESET_HUD_COMPONENT_VALUES, i);
                Function.Call(Hash.DO_SCREEN_FADE_IN, 0);
                Script.Wait(250);
                Function.Call(Hash.DO_SCREEN_FADE_IN, 0);
                Script.Wait(250);
                Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "respawn_controller");
                Function.Call(Hash.IGNORE_NEXT_RESTART, false);
                Function.Call((Hash)0xA2716D40842EAF79); // CLEAR_RESTART_COORD_OVERRIDE
                Function.Call((Hash)0x2C2B3493FBF51C71, false); // Unpause death/arrest restart
                Function.Call((Hash)0xC0AA53F866B3134D); // FORCE_GAME_STATE_PLAYING (late)
                Function.Call(Hash.SET_PLAYER_CONTROL, Game.Player, true, 0);
                Function.Call(Hash.SET_ENTITY_INVINCIBLE, Game.Player.Character, false);
                Function.Call(Hash.DISPLAY_HUD, true);
                Game.TimeScale = 1.0f;
                Function.Call(Hash.NETWORK_OVERRIDE_CLOCK_TIME, currentHour, currentMinute, currentSecond);
                Function.Call(Hash.PAUSE_CLOCK, false);

                HandlePlayerDeath();

                int postDeathCheck = Game.GameTime;
                while (Game.GameTime - postDeathCheck < 5000)
                {
                    if (Vector3.Distance(Game.Player.Character.Position, ringCenter) > 10f || Game.Player.Character.Position.Z > respawnPos.Z + 0.5f)
                    {
                        Game.Player.Character.Position = respawnPos;
                        Function.Call(Hash.SET_ENTITY_VELOCITY, Game.Player.Character, 0f, 0f, -1f);
                        Function.Call(Hash.SET_ENTITY_COLLISION, Game.Player.Character, true, true);
                        Function.Call(Hash.CLEAR_PED_TASKS_IMMEDIATELY, Game.Player.Character);
                        Function.Call(Hash.DO_SCREEN_FADE_IN, 0);
                    }
                    Function.Call(Hash.NETWORK_OVERRIDE_CLOCK_TIME, currentHour, currentMinute, currentSecond);
                    Game.TimeScale = 1.0f;
                    Script.Yield();
                }

                return;
            }
        }

        if (bettingActive && !playerFighting)
        {
            if (Game.GameTime - bettingWindowStartTime >= choiceWindowTimer && choosingMode)
            {
                choosingMode = false;
                Notification.PostTicker("Choice time up! Bet on Red (←) or Blue (→).", true, true);
            }
            if (Game.GameTime - bettingWindowStartTime >= bettingWindowTimer)
            {
                bettingActive = false;
                enteringBet = false;
                Notification.PostTicker("Betting window closed! The fight begins.", true, true);
                BeginFight();
            }
        }

        if (bettingActive && fightParticipants.Count == 2)
        {
            if (currentFighter != null && currentFighter.Ped.Exists())
            {
                Vector3 markerPos = currentFighter.Ped.Position + new Vector3(0f, 0f, 1.5f);
                Function.Call(Hash.DRAW_MARKER, 1, markerPos.X, markerPos.Y, markerPos.Z, 0f, 0f, 0f, 0f, 0f, 0f, 0.5f, 0.5f, 0.5f, 255, 0, 0, 100, false, true, 2, false, 0, 0, false);
            }
            if (challenger != null && challenger.Ped.Exists())
            {
                Vector3 markerPos = challenger.Ped.Position + new Vector3(0f, 0f, 1.5f);
                Function.Call(Hash.DRAW_MARKER, 1, markerPos.X, markerPos.Y, markerPos.Z, 0f, 0f, 0f, 0f, 0f, 0f, 0.5f, 0.5f, 0.5f, 0, 0, 255, 100, false, true, 2, false, 0, 0, false);
            }
            GTA.UI.Screen.ShowSubtitle("Red: " + currentFighter.Nickname + " (" + currentFighter.Gang + ", " + currentFighter.Wins + "-" + currentFighter.Losses + ") | Blue: " + challenger.Nickname + " (" + challenger.Gang + ", " + challenger.Wins + "-" + challenger.Losses + ") | Bet: $" + betAmount + " | Your Record: " + playerWins + "-" + playerLosses, 100);
        }

        if (promptingContinue && Game.GameTime - promptStartTime >= promptDuration)
        {
            promptingContinue = false;
            StopFighting();
        }

        if (fightParticipants.Count > 1 && (IsFighterDeadOrUnconscious(fightParticipants[0].Ped) || IsFighterDeadOrUnconscious(fightParticipants[1].Ped)))
        {
            EndFight();
        }
    }

    private bool IsFighterDeadOrUnconscious(Ped fighter)
    {
        return fighter == null || !fighter.Exists() || fighter.Health <= 0 || fighter.IsInjured;
    }

    private void EndFight()
    {
        bettingActive = false;
        Notification.PostTicker("Fight is over!", true, true);

        winnerFighter = null;
        loserFighter = null;
        if (!IsFighterDeadOrUnconscious(fightParticipants[0].Ped))
        {
            winnerFighter = fightParticipants[0];
            loserFighter = fightParticipants[1];
        }
        else if (!IsFighterDeadOrUnconscious(fightParticipants[1].Ped))
        {
            winnerFighter = fightParticipants[1];
            loserFighter = fightParticipants[0];
        }

        if (winnerFighter != null) winnerFighter.Wins++;
        if (loserFighter != null) loserFighter.Losses++;

        if (playerFighting)
        {
            if (winnerFighter != null && winnerFighter.Ped == Game.Player.Character)
            {
                playerWins++;
                int winnings = 100 + (playerWinStreak * 50);
                Game.Player.Money += winnings;
                totalEarnings += winnings;

                if (playerWinStreak >= bossFightThreshold)
                {
                    Notification.PostTicker(string.Format("You defeated {0} the boss! Earned ${1}. Streak reset. Press Y to heal (${2}) and continue, U to continue as-is, N to stop (15s).", winnerFighter.Nickname, winnings, healCost), true, true);
                    playerWinStreak = 0;
                }
                else
                {
                    Notification.PostTicker(string.Format("You beat {0} from {1}! Earned ${2}. Press Y to heal (${3}) and continue, U to continue as-is, N to stop (15s).", loserFighter.Nickname, loserFighter.Gang, winnings, healCost), true, true);
                }
                promptingContinue = true;
                promptStartTime = Game.GameTime;
            }
            else if (loserFighter != null && loserFighter.Ped == Game.Player.Character)
            {
                playerLosses++;
                playerWinStreak = 0;
                Notification.PostTicker("You lost to " + winnerFighter.Nickname + " from " + winnerFighter.Gang + "! Respawning...", true, true);
            }
        }
        else if (bettedFighter != null && bettedFighter == winnerFighter)
        {
            int odds = GetOdds(fightParticipants.IndexOf(bettedFighter));
            int winnings = betAmount * odds;
            Game.Player.Money += winnings;
            totalEarnings += winnings;
            Notification.PostTicker("You won $" + winnings + " betting on " + bettedFighter.Nickname + " from " + bettedFighter.Gang + "!", true, true);
        }
        else if (bettedFighter != null)
        {
            Notification.PostTicker("You lost your bet. " + winnerFighter.Nickname + " from " + winnerFighter.Gang + " won.", true, true);
        }

        if (loserFighter != null && loserFighter.Ped.Exists() && loserFighter.Ped != Game.Player.Character)
        {
            fightParticipants.Remove(loserFighter);
        }

        if (!promptingContinue)
            DelayTimer();
    }

    private void HandlePlayerDeath()
    {
        RestorePlayerWeapons();

        playerLosses++;
        playerWinStreak = 0;

        int penalty = 500;
        if (Game.Player.Money >= penalty)
        {
            Game.Player.Money -= penalty;
            totalEarnings -= penalty;
            Notification.PostTicker("You lost! Fight club penalty: -$" + penalty + ". New round incoming...", true, true);
        }
        else
        {
            Game.Player.Money = 0;
            Notification.PostTicker("You lost! Fight club penalty: Bankrupted! New round incoming...", true, true);
        }

        playerFighting = false;

        Fighter survivingFighter = null;
        if (winnerFighter != null && winnerFighter.Ped.Exists() && !winnerFighter.Ped.IsDead && winnerFighter.Ped != Game.Player.Character)
        {
            survivingFighter = winnerFighter;
            survivingFighter.Ped.Health = survivingFighter.Ped.MaxHealth;
            survivingFighter.Ped.Position = ringCenter + new Vector3(-1.0f, 0, 0);
            survivingFighter.Ped.Heading = 270f;
            Notification.PostTicker(survivingFighter.Nickname + " from " + survivingFighter.Gang + " stands victorious!", true, true);
        }

        foreach (Fighter f in fightParticipants)
        {
            if (f != null && f.Ped.Exists() && f.Ped != Game.Player.Character && f != survivingFighter)
            {
                f.Ped.Delete();
            }
        }
        fightParticipants.Clear();
        if (loserFighter != null && loserFighter.Ped.Exists() && loserFighter.Ped != Game.Player.Character)
        {
            loserFighter.Ped.Delete();
            loserFighter = null;
        }

        if (survivingFighter != null)
        {
            currentFighter = survivingFighter;
            challenger = CreateFighter(true, 1);
        }
        else
        {
            currentFighter = CreateFighter(true, -1);
            challenger = CreateFighter(true, 1);
        }

        if (currentFighter != null && challenger != null)
        {
            fightParticipants.Add(currentFighter);
            fightParticipants.Add(challenger);

            // Added: Reset AI combat targets to fight each other, not the player
            if (currentFighter.Ped.Exists())
            {
                currentFighter.Ped.Task.ClearAll();
                currentFighter.Ped.Task.Combat(challenger.Ped);
            }
            if (challenger.Ped.Exists())
            {
                challenger.Ped.Task.ClearAll();
                challenger.Ped.Task.Combat(currentFighter.Ped);
            }

            fighterOdds[0] = CalculateOdds(currentFighter.Ped.Health);
            fighterOdds[1] = CalculateOdds(challenger.Ped.Health);
            bettingActive = true;
            choosingMode = true;
            bettingWindowStartTime = Game.GameTime;
            Notification.PostTicker("New round! Press Left (←) to bet, Right (→) to fight (15s to choose).", true, true);
        }
        else
        {
            Notification.PostTicker("Failed to spawn fighters. Ending Fight Club.", true, true);
            isFightClubActive = false;
        }

        winnerFighter = null;
        bettedFighter = null;
    }

    private void DelayTimer()
    {
        int delayStartTime = Game.GameTime;
        int cleanupThreshold = 8000;

        while (Game.GameTime - delayStartTime < 10000)
        {
            if (loserFighter != null && loserFighter.Ped.Exists() && loserFighter.Ped != Game.Player.Character && Game.GameTime - delayStartTime >= cleanupThreshold)
            {
                loserFighter.Ped.Delete();
                loserFighter = null;
            }
            Script.Yield();
        }

        if (winnerFighter != null && winnerFighter.Ped.Exists() && !winnerFighter.Ped.IsDead && !winnerFighter.Ped.IsInjured)
        {
            if (winnerFighter.Ped == Game.Player.Character)
            {
                return;
            }
            else
            {
                winnerFighter.Ped.Health = winnerFighter.Ped.MaxHealth;
                winnerFighter.Ped.Position = ringCenter + new Vector3(-1.0f, 0, 0);
                winnerFighter.Ped.Heading = 270f;
                currentFighter = winnerFighter;
                challenger = CreateFighter(true, 1);
                if (challenger != null)
                {
                    fightParticipants.Clear();
                    fightParticipants.Add(currentFighter);
                    fightParticipants.Add(challenger);

                    fighterOdds[0] = CalculateOdds(currentFighter.Ped.Health);
                    fighterOdds[1] = CalculateOdds(challenger.Ped.Health);

                    bettingActive = true;
                    choosingMode = true;
                    bettingWindowStartTime = Game.GameTime;
                    Notification.PostTicker("New round! Press Left (←) to bet, Right (→) to fight (15s to choose).", true, true);
                    bettedFighter = null;
                }
                else
                {
                    StartFightClub(currentArenaName);
                }
            }
        }
        else
        {
            StartFightClub(currentArenaName);
        }
    }

    // Modified: Added parameter to distinguish manual vs. distance-based deactivation
    private void EndFightClub(bool manualDeactivation = true)
    {
        bettingActive = false;
        enteringBet = false;
        playerFighting = false;
        choosingMode = false;
        promptingContinue = false;
        if (loserFighter != null && loserFighter.Ped.Exists() && loserFighter.Ped != Game.Player.Character)
        {
            loserFighter.Ped.Delete();
            loserFighter = null;
        }
        ClearFighters();

        if (isFightClubActive)
        {
            RestorePlayerWeapons();
            playerWeapons.Clear();
            isFightClubActive = false;
            if (manualDeactivation)
            {
                Notification.PostTicker(string.Format("Fight Club manually ended. Stats: Wins: {0}, Losses: {1}, Earnings: ${2}. Move to an activation spot to start again.", playerWins, playerLosses, totalEarnings), true, true);
            }
            else
            {
                Notification.PostTicker(string.Format("Fight Club ended due to distance. Stats: Wins: {0}, Losses: {1}, Earnings: ${2}. Move to an activation spot to start again.", playerWins, playerLosses, totalEarnings), true, true);
            }
        }
        else
        {
            Notification.PostTicker("Fight Club is already ended.", true, true);
        }
    }

    private void ClearFighters()
    {
        foreach (Fighter f in fightParticipants)
        {
            if (f != null && f.Ped.Exists() && f.Ped != Game.Player.Character)
            {
                f.Ped.Delete();
                while (f.Ped.Exists()) Script.Yield();
            }
        }
        foreach (Ped p in crowd)
        {
            if (p != null && p.Exists())
            {
                p.Delete();
                while (p.Exists()) Script.Yield();
            }
        }
        fightParticipants.Clear();
        crowd.Clear();
        currentFighter = null;
        challenger = null;
        winnerFighter = null;
        bettedFighter = null;
    }
}