using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DNA;
using DNA.CastleMinerZ;
using DNA.CastleMinerZ.AI;
using DNA.CastleMinerZ.Inventory;
using DNA.CastleMinerZ.Net;
using DNA.CastleMinerZ.Terrain;
using DNA.CastleMinerZ.UI;
using DNA.Net.GamerServices;
using Microsoft.Xna.Framework;

using static ModLoader.LogSystem;

namespace ChaosMod
{
    public enum ChaosEffectID : byte
    {
        KillPlayer = 0,
        ObliterateEnemies,
        ExplodePlayer,
        RandomBlocks,
        SpawnRandomEnemy,
        SpawnRandomDragon,
        RandomTeleport,
        TeleportToStart,
        TeleportToSurface,
        TeleportToLagoon,
        TeleportToDesert,
        TeleportToMountains,
        TeleportToArctic,
        TeleportToMinersCove,
        TeleportToHellOnEarth,
        AroundTheWorld,
        GiveRandomItem,
        KillAllEnemies,
        LaunchPlayerUp,
        LaunchAllEnemiesUp,
        TeleportAllEnemiesToPlayer,
        SetTimeToDay,
        SetTimeToNight,
        Nothing,
        OpenInventory,
        SpawnLiveGrenade,
        FireRocket,
        GiveHauntedChainsaw,
        GiveDiamondDoor,
        GiveTechDoor,
        DeleteBlocks,
        SpawnRandomFloorOfBlocks,
        GodMode,
        QuakeFOV,
        AirJump,
        NoJump,
        ExplosiveAmmo,
        RestartGame,
        Lava,
        LaunchEveryoneUp,
        Flying,
        Timelapse,
        NoHUD,
        Bloom,
        Bind,
        DarkMode,
        SwitchCurrentTray,
        CorruptedAssaultRifle,
        AttachRandomBlocksToAllEnemies,
        AllEnemiesGiveUp,
        AllEnemiesSpeedUp,
        AttackOnTitan,
        TeleportPlayerToDragon,
        Fullbright,
        Reach,
        InfiniteAmmo,
        NoRecoil,
        FastShot,
        RapidMine,
        RapidKnife,
        RapidPlace,
        BlockBrush,
        NoUWalls,
        NoSprint,
        SuperSprint,
        SetRandomDay,
        BrokenExplosiveAmmo,
        BrokenGun,
        DropAllItems,
        Lightning,
        KillDragon,
        EnemyNameTags,
        DragonNameTags,
        Disco,
        Water,
        CantDig,
        CantBuild,
        CantCraft,
        CantOpenInventory,
        ForceField,
        CantSwitchTrays,
        COUNT
    }

    // Flags read by Harmony patches each frame.
    internal static class ChaosState
    {
        public static bool GodMode;
        public static bool AirJump;
        public static bool NoJump;
        public static bool ExplosiveAmmo;
        public static bool Flying;
        public static bool Timelapse;
        public static bool NoHUD;
        public static bool Bloom;
        public static bool Bind;
        public static bool DarkMode;
        public static bool Fullbright;
        public static bool Reach;
        public static bool InfiniteAmmo;
        public static bool NoRecoil;
        public static bool FastShot;
        public static bool RapidMine;
        public static bool RapidKnife;
        public static bool RapidPlace;
        public static bool BlockBrush;
        public static bool NoSprint;
        public static bool SuperSprint;
        public static bool BrokenExplosiveAmmo;
        public static bool BrokenGun;
        public static bool EnemyNameTags;
        public static bool DragonNameTags;
        public static bool Disco;
        public static bool Water;
        public static bool CantDig;
        public static bool CantBuild;
        public static bool CantCraft;
        public static bool CantOpenInventory;
        public static bool ForceField;
        public static bool CantSwitchTrays;
        public static bool QuakeFOV;

        // Screen notification (kept for external mod compatibility)
        public static string NotificationText = "";

        // Disco sub-timer
        public static float DiscoSubTimer;

        // Force field sub-timer
        public static float ForceFieldSubTimer;
    }

    internal static class ChaosEffects
    {
        private static readonly Random _rng = new Random();
        private static readonly Dictionary<ChaosEffectID, float> _timedEffects = new Dictionary<ChaosEffectID, float>();
        private static float _chaosTimer;
        private static int _lastCountdownSecond = -1;
        private static bool _prevIsChaosMode = false;
        private static bool _announced = false;
        private static float _settingsEnforceTimer = 0f;  // re-apply settings for this many seconds after start
        // Set the moment the player clicks "Chaos" in the menu; cleared once the world finishes loading.
        private static bool _chaosSelected = false;
        private static int _tickLogCounter = 0;
        private static int _startupLogCounter = 0;
        private static float _savedDay = -1f; // for RandomDay effect restore

        // ── On-screen chat-style message queue (drawn by GamePatches HUD postfix) ──
        public struct ChatLine { public string Text; public float TimeLeft; public Microsoft.Xna.Framework.Color Color; }
        public static readonly System.Collections.Generic.Queue<ChatLine> ChatQueue =
            new System.Collections.Generic.Queue<ChatLine>();
        public static readonly object ChatLock = new object();

        public static void PostChat(string text, Microsoft.Xna.Framework.Color color)
        {
            lock (ChatLock)
            {
                if (ChatQueue.Count > 8) ChatQueue.Dequeue(); // cap so screen doesn't fill up
                ChatQueue.Enqueue(new ChatLine { Text = text, TimeLeft = 5f, Color = color });
            }
        }

        public static void TickChatLines(float dt)
        {
            lock (ChatLock)
            {
                // Trim expired lines from the front
                while (ChatQueue.Count > 0)
                {
                    var peek = ChatQueue.Peek();
                    // We can't mutate structs in the queue directly — rebuild from a list
                    break;
                }
                // Rebuild with updated timers
                var list = new System.Collections.Generic.List<ChatLine>(ChatQueue);
                ChatQueue.Clear();
                foreach (var line in list)
                {
                    var updated = new ChatLine { Text = line.Text, TimeLeft = line.TimeLeft - dt, Color = line.Color };
                    if (updated.TimeLeft > 0f)
                        ChatQueue.Enqueue(updated);
                }
            }
        }

        // Called once at mod load.
        public static void Initialize()
        {
            Log("[ChaosMod] ChaosEffects initialized - VERSION 2026-05-01-13:30");
        }

        public static void Init()
        {
            _chaosTimer = ChaosSettings.ChaosInterval;
            _lastCountdownSecond = -1;
            _prevIsChaosMode = false;
            _announced = false;
            _chaosSelected = false;
            _settingsEnforceTimer = 0f;
            _timedEffects.Clear();
            _isSyncExecution = false;
            _ownSyncMessages.Clear();
            _heartbeatTimer = 0f;
            _originalSprintSpeed = -1f;
            _sprintFieldName = null;
            _cachedSprintField = null;
            lock (ChatLock) { ChatQueue.Clear(); }
        }

        /// <summary>
        /// Call this the instant the player picks "Chaos" in the game-mode menu.
        /// It arms the pending flag so Tick() activates chaos once the world finishes loading.
        /// </summary>
        public static void MarkChaosSelected()
        {
            _chaosSelected = true;
            ChaosSettings.IsChaosMode = true; // set eagerly; Tick will confirm once session is up
            _announced = false;
            _chaosTimer = ChaosSettings.ChaosInterval;
            _lastCountdownSecond = -1;
            lock (ChatLock) { ChatQueue.Clear(); }
            Log("[ChaosMod] Chaos mode selected — waiting for world to finish loading.");
        }

        private static void SendChat(string msg) => SendChat(msg, Microsoft.Xna.Framework.Color.Yellow);
        private static void SendChat(string msg, Microsoft.Xna.Framework.Color color)
        {
            PostChat(msg, color);
        }

        /// <summary>
        /// Broadcasts a message through the game's real network chat (BroadcastTextMessage)
        /// so every player in the session sees it in their chat window.
        /// Confirmed API from ChatInputScreen.cs source:
        ///   BroadcastTextMessage.Send(game.MyNetworkGamer, text)
        /// </summary>
        private static void TrySendGameChat(string message)
        {
            try
            {
                var game = CastleMinerZGame.Instance;
                if (game?.MyNetworkGamer == null) return;
                // Prefix with [ChaosMod] so it's clear where the message comes from in chat.
                BroadcastTextMessage.Send(game.MyNetworkGamer, message);
            }
            catch { }
        }

        public static void Tick(float dt)
        {
            if (!ChaosSettings.Enabled)
                return;

            var game = CastleMinerZGame.Instance;
            bool sessionUp = game?.LocalPlayer != null && (game.CurrentNetworkSession == null || game.MyNetworkGamer != null);

            // ── Pending activation: player clicked "Chaos" in the menu ───────
            // Stay here (do nothing) until the world finishes loading.
            if (_chaosSelected)
            {
                if (!sessionUp) return; // still loading — keep waiting

                // World is ready — activate chaos now.
                ChaosSettings.IsChaosMode = true;
                _chaosSelected = false;
                Log("[ChaosMod] World loaded — chaos mode now active.");
            }

            // ── Detect IsChaosMode turning OFF (returned to main menu) ───────
            if (_prevIsChaosMode && !ChaosSettings.IsChaosMode)
            {
                _chaosTimer = ChaosSettings.ChaosInterval;
                _lastCountdownSecond = -1;
                _announced = false;
            }
            _prevIsChaosMode = ChaosSettings.IsChaosMode;

            if (!ChaosSettings.IsChaosMode) return;

            // ── No live session ───────────────────────────────────────────────
            if (!sessionUp)
            {
                // If chaos was already running and the session dropped, clean up.
                if (_announced)
                {
                    ChaosSettings.IsChaosMode = false;
                    _announced = false;
                    _lastCountdownSecond = -1;
                    _chaosTimer = ChaosSettings.ChaosInterval;
                }
                return;
            }

            // Tick the on-screen chat line timers.
            TickChatLines(dt);

            bool isHost = IsLocalHost();
            if (!isHost)
            {
                Log("[ChaosMod] Tick: Not host, skipping timer/TriggerRandom");
            }

            if (isHost)
            {
                // One-time startup announcement.
                if (!_announced)
                {
                    if (++_startupLogCounter % 300 == 0)
                        Log($"[ChaosMod] Tick: Startup block hit, _announced={_announced}, IsChaosMode={ChaosSettings.IsChaosMode}");

                    _announced = true;
                    _chaosTimer = ChaosSettings.ChaosInterval;
                    _heartbeatTimer = 15f; // first heartbeat 15s after start
                    _lastCountdownSecond = -1;
                    _settingsEnforceTimer = 8f;
                    int intervalSecs = (int)ChaosSettings.ChaosInterval;
                    string startMsg = $"=== CHAOS MODE ACTIVE! Effect every {intervalSecs}s. Survive! ===";
                    SendChat(startMsg, Microsoft.Xna.Framework.Color.OrangeRed);
                    TrySendGameChat(startMsg);
                    BroadcastChaosStart(); // tell all clients with ChaosMod to activate
                    Log("[ChaosMod] Tick: Startup announcement sent");
                }

                // Heartbeat: only the HOST re-broadcasts so clients that join mid-session activate.
                // Clients don't need to heartbeat — the host already covers everyone.
                _heartbeatTimer -= dt;
                if (_heartbeatTimer <= 0f)
                {
                    _heartbeatTimer = 15f;
                    BroadcastChaosStart();
                }

                // Keep enforcing Survival/Hardcore for the first few seconds so other mods can't override us.
                if (_settingsEnforceTimer > 0f)
                {
                    _settingsEnforceTimer -= dt;
                    try
                    {
                        var g = CastleMinerZGame.Instance;
                        if (g != null)
                        {
                            if (g.GameMode != DNA.CastleMinerZ.UI.GameModeTypes.Survival)
                                g.GameMode = DNA.CastleMinerZ.UI.GameModeTypes.Survival;
                            if (g.Difficulty != DNA.CastleMinerZ.UI.GameDifficultyTypes.HARDCORE)
                                g.Difficulty = DNA.CastleMinerZ.UI.GameDifficultyTypes.HARDCORE;
                            g.InfiniteResourceMode = false;
                        }
                    }
                    catch { }
                }

                // Count down to next random effect.
                _chaosTimer -= dt;

                // Log timer every 5 seconds for debugging
                if (++_tickLogCounter % 300 == 0) // every ~5 seconds at 60fps
                {
                    Log($"[ChaosMod] Tick: Timer at {_chaosTimer:F2}s, announced={_announced}");
                }

                // Show countdown in chat for the last 5 seconds.
                if (_chaosTimer <= 5f && _chaosTimer > 0f)
                {
                    int secondsLeft = (int)Math.Ceiling(_chaosTimer);
                    if (secondsLeft != _lastCountdownSecond)
                    {
                        _lastCountdownSecond = secondsLeft;
                        SendChat($"[Chaos] Effect in {secondsLeft}...", Microsoft.Xna.Framework.Color.Yellow);
                        Log($"[ChaosMod] Tick: Countdown {secondsLeft}s");
                    }
                }

                if (_chaosTimer <= 0f)
                {
                    _chaosTimer = ChaosSettings.ChaosInterval;
                    _lastCountdownSecond = -1;
                    Log("[ChaosMod] Tick: Timer expired, calling TriggerRandom()");
                    TriggerRandom();
                }
            }
            else
            {
                _announced = true;
                _lastCountdownSecond = -1;
            }

            // Update all active timed effects.
            TickTimedEffects(dt);

            // Re-enforce per-tick effects that the game's physics/systems can reset.
            try
            {
                if (ChaosState.NoSprint) SetSprintField(0.1f);
                else if (ChaosState.SuperSprint) SetSprintField(10f);
                if (ChaosState.Flying) SetFlying(true); // FlyMode gets cleared by game when not in Creative
            }
            catch { }

            // Continuous timed effect sub-timers.
            if (ChaosState.Disco)
            {
                ChaosState.DiscoSubTimer -= dt;
                if (ChaosState.DiscoSubTimer <= 0f)
                {
                    ChaosState.DiscoSubTimer = 0.5f;
                    SetRandomSkyColor();
                }
            }

            if (ChaosState.ForceField)
            {
                ChaosState.ForceFieldSubTimer -= dt;
                if (ChaosState.ForceFieldSubTimer <= 0f)
                {
                    ChaosState.ForceFieldSubTimer = 0.25f;
                    DeleteBlocksAroundPlayer(10);
                }
            }

            if (ChaosState.Timelapse)
            {
                // Fast-forward time: every tick, advance by extra amount.
                try
                {
                    if (game?.GameScreen != null)
                    {
                        var gs = game.GameScreen;
                        float tod = gs.TimeOfDay;
                        tod += dt * (1f / 1f); // 1-second full cycle means advance by dt * 1.0 extra
                        if (tod >= 1f) tod -= 1f;
                        SetTimeOfDayLocal(gs, tod);
                    }
                }
                catch { }
            }
        }

        private static void TickTimedEffects(float dt)
        {
            var toRemove = new List<ChaosEffectID>();
            var keys = new List<ChaosEffectID>(_timedEffects.Keys);
            foreach (var id in keys)
            {
                _timedEffects[id] -= dt;
                if (_timedEffects[id] <= 0f)
                    toRemove.Add(id);
            }
            foreach (var id in toRemove)
                _timedEffects.Remove(id);

            RefreshStateFlags();

            // Revert expired effects that need cleanup.
            foreach (var id in toRemove)
                OnEffectExpired(id);
        }

        private static void RefreshStateFlags()
        {
            ChaosState.GodMode             = _timedEffects.ContainsKey(ChaosEffectID.GodMode);
            ChaosState.AirJump             = _timedEffects.ContainsKey(ChaosEffectID.AirJump);
            ChaosState.NoJump              = _timedEffects.ContainsKey(ChaosEffectID.NoJump);
            ChaosState.ExplosiveAmmo       = _timedEffects.ContainsKey(ChaosEffectID.ExplosiveAmmo);
            ChaosState.Flying              = _timedEffects.ContainsKey(ChaosEffectID.Flying);
            ChaosState.Timelapse           = _timedEffects.ContainsKey(ChaosEffectID.Timelapse);
            ChaosState.NoHUD               = _timedEffects.ContainsKey(ChaosEffectID.NoHUD);
            ChaosState.Bloom               = _timedEffects.ContainsKey(ChaosEffectID.Bloom);
            ChaosState.Bind                = _timedEffects.ContainsKey(ChaosEffectID.Bind);
            ChaosState.DarkMode            = _timedEffects.ContainsKey(ChaosEffectID.DarkMode);
            ChaosState.Fullbright          = _timedEffects.ContainsKey(ChaosEffectID.Fullbright);
            ChaosState.Reach               = _timedEffects.ContainsKey(ChaosEffectID.Reach);
            ChaosState.InfiniteAmmo        = _timedEffects.ContainsKey(ChaosEffectID.InfiniteAmmo);
            ChaosState.NoRecoil            = _timedEffects.ContainsKey(ChaosEffectID.NoRecoil);
            ChaosState.FastShot            = _timedEffects.ContainsKey(ChaosEffectID.FastShot);
            ChaosState.RapidMine           = _timedEffects.ContainsKey(ChaosEffectID.RapidMine);
            ChaosState.RapidKnife          = _timedEffects.ContainsKey(ChaosEffectID.RapidKnife);
            ChaosState.RapidPlace          = _timedEffects.ContainsKey(ChaosEffectID.RapidPlace);
            ChaosState.BlockBrush          = _timedEffects.ContainsKey(ChaosEffectID.BlockBrush);
            ChaosState.NoSprint            = _timedEffects.ContainsKey(ChaosEffectID.NoSprint);
            ChaosState.SuperSprint         = _timedEffects.ContainsKey(ChaosEffectID.SuperSprint);
            ChaosState.BrokenExplosiveAmmo = _timedEffects.ContainsKey(ChaosEffectID.BrokenExplosiveAmmo);
            ChaosState.BrokenGun           = _timedEffects.ContainsKey(ChaosEffectID.BrokenGun);
            ChaosState.EnemyNameTags       = _timedEffects.ContainsKey(ChaosEffectID.EnemyNameTags);
            ChaosState.DragonNameTags      = _timedEffects.ContainsKey(ChaosEffectID.DragonNameTags);
            ChaosState.Disco               = _timedEffects.ContainsKey(ChaosEffectID.Disco);
            ChaosState.Water               = _timedEffects.ContainsKey(ChaosEffectID.Water);
            ChaosState.CantDig             = _timedEffects.ContainsKey(ChaosEffectID.CantDig);
            ChaosState.CantBuild           = _timedEffects.ContainsKey(ChaosEffectID.CantBuild);
            ChaosState.CantCraft           = _timedEffects.ContainsKey(ChaosEffectID.CantCraft);
            ChaosState.CantOpenInventory   = _timedEffects.ContainsKey(ChaosEffectID.CantOpenInventory);
            ChaosState.ForceField          = _timedEffects.ContainsKey(ChaosEffectID.ForceField);
            ChaosState.CantSwitchTrays     = _timedEffects.ContainsKey(ChaosEffectID.CantSwitchTrays);
            ChaosState.QuakeFOV            = _timedEffects.ContainsKey(ChaosEffectID.QuakeFOV);
        }

        private static void OnEffectExpired(ChaosEffectID id)
        {
            try
            {
                switch (id)
                {
                    case ChaosEffectID.DarkMode:
                    case ChaosEffectID.Fullbright:
                        RestoreAmbientLight();
                        break;
                    case ChaosEffectID.Water:
                        SetWaterPlane(false);
                        break;
                    case ChaosEffectID.QuakeFOV:
                        RestoreFOV();
                        break;
                    case ChaosEffectID.SetRandomDay:
                        RestoreDay();
                        break;
                    case ChaosEffectID.SuperSprint:
                    case ChaosEffectID.NoSprint:
                        RestoreSprintSpeed();
                        break;
                    case ChaosEffectID.Flying:
                        SetFlying(false);
                        break;
                    case ChaosEffectID.Reach:
                        RestoreReach();
                        break;
                }
            }
            catch { }
        }

        public static void TriggerRandom()
        {
            int count = (int)ChaosEffectID.COUNT;
            var id = (ChaosEffectID)_rng.Next(0, count);
            TriggerEffect(id);
        }

        public static void TriggerEffect(ChaosEffectID id)
        {
            try
            {
                string name = GetEffectName(id);
                ShowNotification(name);
                Log($"[ChaosMod] Triggering effect: {name}");
                ExecuteEffect(id);
            }
            catch (Exception ex)
            {
                Log($"[ChaosMod] Effect {id} failed: {ex.Message}");
            }
        }

        private static void ActivateTimed(ChaosEffectID id)
        {
            _timedEffects[id] = ChaosSettings.EffectDuration;
            RefreshStateFlags();
        }

        private static void ShowNotification(string text)
        {
            if (!ChaosSettings.ShowNotifications) return;
            string msg = $"[Chaos] {text}!";
            SendChat(msg, Microsoft.Xna.Framework.Color.Lime);
            TrySendGameChat(msg); // also show in the real in-game chat for all players
        }

        // ─────────────────────────────────────────────────────────────────────
        // Effect result logging
        // ─────────────────────────────────────────────────────────────────────

        private static readonly string _effectLogPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "!Mods", "ChaosMod", "ChaosEffects.log");

        private static void AppendEffectLog(string line)
        {
            try
            {
                // Trim the log file to the last 500 lines if it gets too big.
                if (File.Exists(_effectLogPath))
                {
                    string[] existing = File.ReadAllLines(_effectLogPath);
                    if (existing.Length > 1000)
                    {
                        var trimmed = new string[500];
                        Array.Copy(existing, existing.Length - 500, trimmed, 0, 500);
                        File.WriteAllLines(_effectLogPath, trimmed);
                    }
                }
                File.AppendAllText(_effectLogPath,
                    $"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
            }
            catch { }
        }

        // ── Multiplayer sync ────────────────────────────────────────────────────
        // Host fires an effect → sends "CHAOSMOD|EffectName" via BroadcastTextMessage.
        // Every client with ChaosMod receives it in Patch_ChaosSyncMessage (GamePatches)
        // and calls ExecuteSyncEffect() to run the same effect locally.
        // _isSyncExecution guards against re-broadcasting from within a synced execution
        // (which would create an infinite broadcast loop between all clients).
        private static bool _isSyncExecution = false;
        private static readonly HashSet<string> _ownSyncMessages = new HashSet<string>();
        private static float _heartbeatTimer = 0f;   // broadcasts CHAOS_START so late joiners activate

        private static bool IsLocalHost()
        {
            try
            {
                var game = CastleMinerZGame.Instance;
                if (game?.LocalPlayer == null)
                    return false;
                if (game.CurrentNetworkSession == null)
                    return true;
                return game.MyNetworkGamer != null && game.MyNetworkGamer.IsHost;
            }
            catch { return false; }
        }

        private static void TrySyncEffectToClients(string effectName)
        {
            if (_isSyncExecution) return; // already running a synced effect — don't re-broadcast
            try
            {
                var game = CastleMinerZGame.Instance;
                if (game?.MyNetworkGamer == null || game.CurrentNetworkSession == null) return;
                // BroadcastTextMessage.Echo = false, so host does NOT receive its own message.
                // _ownSyncMessages guard is kept for safety in case any future code changes that.
                _ownSyncMessages.Add(effectName);
                BroadcastTextMessage.Send(game.MyNetworkGamer, "CHAOSMOD|" + effectName);
            }
            catch { }
        }

        public static void SyncChaosStateForJoinedPlayer()
        {
            try
            {
                if (!ChaosSettings.IsChaosMode || !IsLocalHost()) return;
                BroadcastChaosStart();
                foreach (ChaosEffectID id in new List<ChaosEffectID>(_timedEffects.Keys))
                    TrySyncEffectToClients(id.ToString());
            }
            catch { }
        }

        /// <summary>
        /// Called when a CHAOSMOD|CHAOS_START broadcast arrives on a client.
        /// Activates chaos mode locally so this client runs Tick() and sees effects.
        /// </summary>
        public static void ActivateChaosFromRemote()
        {
            bool wasActive = ChaosSettings.IsChaosMode;
            ChaosSettings.IsChaosMode = true;
            _chaosSelected = false;
            _announced = true;
            // Only reset timer if chaos wasn't already active (for late joiners).
            // Don't reset on host's own heartbeat.
            if (!wasActive)
            {
                _chaosTimer = ChaosSettings.ChaosInterval;
                _lastCountdownSecond = -1;
                _settingsEnforceTimer = 0f;
            }
            if (wasActive) return;
            var game = CastleMinerZGame.Instance;
            string modeName = "Unknown";
            try { modeName = game?.GameMode.ToString() ?? "Unknown"; } catch { }
            string msg = $"[Chaos] Joined a CHAOS MODE session! ({modeName})";
            SendChat(msg, Microsoft.Xna.Framework.Color.OrangeRed);
            TrySendGameChat(msg);
            Log("[ChaosMod] Chaos mode activated from remote host.");
        }

        /// <summary>Called when a CHAOSMOD|CHAOS_STOP broadcast arrives on a client.</summary>
        public static void DeactivateChaosFromRemote()
        {
            ChaosSettings.IsChaosMode = false;
            _announced = false;
            foreach (ChaosEffectID id in new List<ChaosEffectID>(_timedEffects.Keys))
                OnEffectExpired(id);
            _timedEffects.Clear();
            RefreshStateFlags();
            SendChat("[Chaos] Chaos mode ended.", Microsoft.Xna.Framework.Color.Gray);
            Log("[ChaosMod] Chaos mode deactivated by remote host.");
        }

        /// <summary>
        /// Broadcasts CHAOSMOD|CHAOS_START so any client with ChaosMod installed
        /// auto-activates — used as both initial announcement and periodic heartbeat.
        /// Not routed through RunEffect so it doesn't fire an effect on receivers.
        /// </summary>
        private static void BroadcastChaosStart()
        {
            try
            {
                var game = CastleMinerZGame.Instance;
                if (game?.MyNetworkGamer == null || game.CurrentNetworkSession == null) return;
                BroadcastTextMessage.Send(game.MyNetworkGamer, "CHAOSMOD|CHAOS_START");
            }
            catch { }
        }

        /// <summary>Called by GamePatches when a remote sync message arrives.</summary>
        public static void ExecuteSyncEffect(ChaosEffectID id)
        {
            string name = id.ToString();
            if (_ownSyncMessages.Remove(name)) return; // our own echo — skip

            Log($"[ChaosMod] ExecuteSyncEffect: {name} (IsLocalHost={IsLocalHost()})");

            // Activate chaos mode on this client so Tick() runs — needed for timed effects,
            // sprint re-enforcement, and any other per-frame continuous effect logic.
            if (!ChaosSettings.IsChaosMode)
            {
                ChaosSettings.IsChaosMode = true;
                _announced = true; // skip the startup announcement since we're a follower client
            }

            AppendEffectLog($"[SYNC] {name}");
            _isSyncExecution = true;
            try { ExecuteEffect(id); }
            catch (Exception ex) { AppendEffectLog($"[SYNC FAIL] {name}: {ex.Message}"); }
            finally { _isSyncExecution = false; }
        }

        /// <summary>
        /// Fires an effect, logging FIRE OK/FAIL to ChaosEffects.log, then syncs to all clients.
        /// </summary>
        private static void RunEffect(ChaosEffectID id, string name, Action action)
        {
            AppendEffectLog($"[FIRE] {name}");
            try
            {
                action();
                AppendEffectLog($"[ OK ] {name}");
                TrySyncEffectToClients(id.ToString()); // tell every other client to run the same effect
            }
            catch (Exception ex)
            {
                AppendEffectLog($"[FAIL] {name}: {ex.GetType().Name} — {ex.Message}");
                Log($"[ChaosMod] Effect {name} threw: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Effect dispatcher
        // ─────────────────────────────────────────────────────────────────────

        private static void ExecuteEffect(ChaosEffectID id)
        {
            switch (id)
            {
                case ChaosEffectID.KillPlayer:                   RunEffect(id, "KillPlayer",                   EffectKillPlayer);                    break;
                case ChaosEffectID.ObliterateEnemies:            RunEffect(id, "ObliterateEnemies",            EffectObliterateEnemies);             break;
                case ChaosEffectID.ExplodePlayer:                RunEffect(id, "ExplodePlayer",                EffectExplodePlayer);                 break;
                case ChaosEffectID.RandomBlocks:                 RunEffect(id, "RandomBlocks",                 EffectRandomBlocks);                  break;
                case ChaosEffectID.SpawnRandomEnemy:             RunEffect(id, "SpawnRandomEnemy",             EffectSpawnRandomEnemy);              break;
                case ChaosEffectID.SpawnRandomDragon:            RunEffect(id, "SpawnRandomDragon",            EffectSpawnRandomDragon);             break;
                case ChaosEffectID.RandomTeleport:               RunEffect(id, "RandomTeleport",               EffectRandomTeleport);                break;
                case ChaosEffectID.TeleportToStart:              RunEffect(id, "TeleportToStart",              EffectTeleportToStart);               break;
                case ChaosEffectID.TeleportToSurface:            RunEffect(id, "TeleportToSurface",            EffectTeleportToSurface);             break;
                case ChaosEffectID.TeleportToLagoon:             RunEffect(id, "TeleportToLagoon",             () => EffectTeleportBiome(250f));     break;
                case ChaosEffectID.TeleportToDesert:             RunEffect(id, "TeleportToDesert",             () => EffectTeleportBiome(1000f));    break;
                case ChaosEffectID.TeleportToMountains:          RunEffect(id, "TeleportToMountains",          () => EffectTeleportBiome(1650f));    break;
                case ChaosEffectID.TeleportToArctic:             RunEffect(id, "TeleportToArctic",             () => EffectTeleportBiome(2350f));    break;
                case ChaosEffectID.TeleportToMinersCove:         RunEffect(id, "TeleportToMinersCove",         () => EffectTeleportBiome(3000f));    break;
                case ChaosEffectID.TeleportToHellOnEarth:        RunEffect(id, "TeleportToHellOnEarth",        () => EffectTeleportBiome(3400f));    break;
                case ChaosEffectID.AroundTheWorld:               RunEffect(id, "AroundTheWorld",               () => EffectTeleportBiome(5000f));    break;
                case ChaosEffectID.GiveRandomItem:               RunEffect(id, "GiveRandomItem",               EffectGiveRandomItem);                break;
                case ChaosEffectID.KillAllEnemies:               RunEffect(id, "KillAllEnemies",               EffectKillAllEnemies);                break;
                case ChaosEffectID.LaunchPlayerUp:               RunEffect(id, "LaunchPlayerUp",               EffectLaunchPlayerUp);                break;
                case ChaosEffectID.LaunchAllEnemiesUp:           RunEffect(id, "LaunchAllEnemiesUp",           EffectLaunchAllEnemiesUp);            break;
                case ChaosEffectID.TeleportAllEnemiesToPlayer:   RunEffect(id, "TeleportAllEnemiesToPlayer",   EffectTeleportAllEnemiesToPlayer);    break;
                case ChaosEffectID.SetTimeToDay:                 RunEffect(id, "SetTimeToDay",                 EffectSetTimeToDay);                  break;
                case ChaosEffectID.SetTimeToNight:               RunEffect(id, "SetTimeToNight",               EffectSetTimeToNight);                break;
                case ChaosEffectID.Nothing:                      AppendEffectLog("[ OK ] Nothing (intentional)");                                   break;
                case ChaosEffectID.OpenInventory:                RunEffect(id, "OpenInventory",                EffectOpenInventory);                 break;
                case ChaosEffectID.SpawnLiveGrenade:             RunEffect(id, "SpawnLiveGrenade",             EffectSpawnLiveGrenade);              break;
                case ChaosEffectID.FireRocket:                   RunEffect(id, "FireRocket",                   EffectFireRocket);                    break;
                case ChaosEffectID.GiveHauntedChainsaw:          RunEffect(id, "GiveHauntedChainsaw",          () => EffectGiveItem(InventoryItemIDs.Chainsaw1, 1)); break;
                case ChaosEffectID.GiveDiamondDoor:              RunEffect(id, "GiveDiamondDoor",              () => EffectGiveItemByName("DiamondDoor", 1));     break;
                case ChaosEffectID.GiveTechDoor:                 RunEffect(id, "GiveTechDoor",                 () => EffectGiveItemByName("TechDoor", 1));         break;
                case ChaosEffectID.DeleteBlocks:                 RunEffect(id, "DeleteBlocks",                 EffectDeleteBlocks);                  break;
                case ChaosEffectID.SpawnRandomFloorOfBlocks:     RunEffect(id, "SpawnRandomFloorOfBlocks",     EffectSpawnRandomFloor);              break;
                case ChaosEffectID.GodMode:                      RunEffect(id, "GodMode",                      () => ActivateTimed(id));             break;
                case ChaosEffectID.QuakeFOV:                     RunEffect(id, "QuakeFOV",                     EffectQuakeFOV);                      break;
                case ChaosEffectID.AirJump:                      RunEffect(id, "AirJump",                      () => ActivateTimed(id));             break;
                case ChaosEffectID.NoJump:                       RunEffect(id, "NoJump",                       () => ActivateTimed(id));             break;
                case ChaosEffectID.ExplosiveAmmo:                RunEffect(id, "ExplosiveAmmo",                () => ActivateTimed(id));             break;
                case ChaosEffectID.RestartGame:                  RunEffect(id, "RestartGame",                  EffectRestartGame);                   break;
                case ChaosEffectID.Lava:                         RunEffect(id, "Lava",                         EffectLava);                          break;
                case ChaosEffectID.LaunchEveryoneUp:             RunEffect(id, "LaunchEveryoneUp",             () => { EffectLaunchPlayerUp(); EffectLaunchAllEnemiesUp(); }); break;
                case ChaosEffectID.Flying:                       RunEffect(id, "Flying",                       EffectFlying);                        break;
                case ChaosEffectID.Timelapse:                    RunEffect(id, "Timelapse",                    () => ActivateTimed(id));             break;
                case ChaosEffectID.NoHUD:                        RunEffect(id, "NoHUD",                        () => ActivateTimed(id));             break;
                case ChaosEffectID.Bloom:                        RunEffect(id, "Bloom",                        () => ActivateTimed(id));             break;
                case ChaosEffectID.Bind:                         RunEffect(id, "Bind",                         () => ActivateTimed(id));             break;
                case ChaosEffectID.DarkMode:                     RunEffect(id, "DarkMode",                     EffectDarkMode);                      break;
                case ChaosEffectID.SwitchCurrentTray:            RunEffect(id, "SwitchCurrentTray",            EffectSwitchCurrentTray);             break;
                case ChaosEffectID.CorruptedAssaultRifle:        RunEffect(id, "CorruptedAssaultRifle",        EffectCorruptedAssaultRifle);         break;
                case ChaosEffectID.AttachRandomBlocksToAllEnemies: RunEffect(id, "AttachBlocksToEnemies",      EffectAttachBlocksToEnemies);         break;
                case ChaosEffectID.AllEnemiesGiveUp:             RunEffect(id, "AllEnemiesGiveUp",             EffectAllEnemiesGiveUp);              break;
                case ChaosEffectID.AllEnemiesSpeedUp:            RunEffect(id, "AllEnemiesSpeedUp",            EffectAllEnemiesSpeedUp);             break;
                case ChaosEffectID.AttackOnTitan:                RunEffect(id, "AttackOnTitan",                EffectAttackOnTitan);                 break;
                case ChaosEffectID.TeleportPlayerToDragon:       RunEffect(id, "TeleportPlayerToDragon",       EffectTeleportPlayerToDragon);        break;
                case ChaosEffectID.Fullbright:                   RunEffect(id, "Fullbright",                   EffectFullbright);                    break;
                case ChaosEffectID.Reach:                        RunEffect(id, "Reach",                        EffectReach);                         break;
                case ChaosEffectID.InfiniteAmmo:                 RunEffect(id, "InfiniteAmmo",                 () => ActivateTimed(id));             break;
                case ChaosEffectID.NoRecoil:                     RunEffect(id, "NoRecoil",                     () => ActivateTimed(id));             break;
                case ChaosEffectID.FastShot:                     RunEffect(id, "FastShot",                     () => ActivateTimed(id));             break;
                case ChaosEffectID.RapidMine:                    RunEffect(id, "RapidMine",                    () => ActivateTimed(id));             break;
                case ChaosEffectID.RapidKnife:                   RunEffect(id, "RapidKnife",                   () => ActivateTimed(id));             break;
                case ChaosEffectID.RapidPlace:                   RunEffect(id, "RapidPlace",                   () => ActivateTimed(id));             break;
                case ChaosEffectID.BlockBrush:                   RunEffect(id, "BlockBrush",                   () => ActivateTimed(id));             break;
                case ChaosEffectID.NoUWalls:                     RunEffect(id, "NoUWalls",                     EffectNoUWalls);                      break;
                case ChaosEffectID.NoSprint:                     RunEffect(id, "NoSprint",                     EffectNoSprint);                      break;
                case ChaosEffectID.SuperSprint:                  RunEffect(id, "SuperSprint",                  EffectSuperSprint);                   break;
                case ChaosEffectID.SetRandomDay:                 RunEffect(id, "SetRandomDay",                 () => ActivateTimed(id));             break;
                case ChaosEffectID.BrokenExplosiveAmmo:          RunEffect(id, "BrokenExplosiveAmmo",          () => ActivateTimed(id));             break;
                case ChaosEffectID.BrokenGun:                    RunEffect(id, "BrokenGun",                    () => ActivateTimed(id));             break;
                case ChaosEffectID.DropAllItems:                 RunEffect(id, "DropAllItems",                 EffectDropAllItems);                  break;
                case ChaosEffectID.Lightning:                    RunEffect(id, "Lightning",                    EffectLightning);                     break;
                case ChaosEffectID.KillDragon:                   RunEffect(id, "KillDragon",                   EffectKillDragon);                    break;
                case ChaosEffectID.EnemyNameTags:                RunEffect(id, "EnemyNameTags",                () => ActivateTimed(id));             break;
                case ChaosEffectID.DragonNameTags:               RunEffect(id, "DragonNameTags",               () => ActivateTimed(id));             break;
                case ChaosEffectID.Disco:                        RunEffect(id, "Disco",                        EffectDisco);                         break;
                case ChaosEffectID.Water:                        RunEffect(id, "Water",                        EffectWater);                         break;
                case ChaosEffectID.CantDig:                      RunEffect(id, "CantDig",                      () => ActivateTimed(id));             break;
                case ChaosEffectID.CantBuild:                    RunEffect(id, "CantBuild",                    () => ActivateTimed(id));             break;
                case ChaosEffectID.CantCraft:                    RunEffect(id, "CantCraft",                    () => ActivateTimed(id));             break;
                case ChaosEffectID.CantOpenInventory:            RunEffect(id, "CantOpenInventory",            () => ActivateTimed(id));             break;
                case ChaosEffectID.ForceField:                   RunEffect(id, "ForceField",                   EffectForceField);                    break;
                case ChaosEffectID.CantSwitchTrays:              RunEffect(id, "CantSwitchTrays",              () => ActivateTimed(id));             break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static object GetLocalPlayer()
        {
            return CastleMinerZGame.Instance?.LocalPlayer;
        }

        private static Vector3 GetPlayerPosition()
        {
            try { return CastleMinerZGame.Instance.LocalPlayer.LocalPosition; }
            catch { return Vector3.Zero; }
        }

        private static void SetPlayerPosition(Vector3 pos)
        {
            try
            {
                // CastleWalls-confirmed API: LocalPlayer.LocalPosition is a writable property.
                CastleMinerZGame.Instance.LocalPlayer.LocalPosition = pos;
            }
            catch { }
        }

        /// <summary>
        /// Teleports every player to a destination computed per-player.
        /// Local player uses GameScreen.TeleportToLocation (correct API) so physics state is
        /// updated. Remote player objects get LocalPosition set directly — those will be
        /// overwritten by the game's net update, but the sync message will have them execute
        /// this locally on their own machines where IsLocal is true.
        /// </summary>
        private static void TeleportAllPlayers(Func<Player, Vector3> destSelector)
        {
            var game = CastleMinerZGame.Instance;
            foreach (Player player in GetAllPlayers())
            {
                try
                {
                    Vector3 dest = destSelector(player);
                    if (player.IsLocal && game?.GameScreen != null)
                        game.GameScreen.TeleportToLocation(dest, false);
                    else
                        player.LocalPosition = dest;
                }
                catch { }
            }
        }

        private static void SetPlayerVelocity(Vector3 vel)
        {
            try
            {
                var game = CastleMinerZGame.Instance;
                if (game?.GameScreen == null) return;
                var pos = GetPlayerPosition();
                // TeleportToLocation takes Vector3 (confirmed from source)
                game.GameScreen.TeleportToLocation(
                    new Vector3(pos.X, pos.Y + vel.Y * 0.3f, pos.Z), false);
            }
            catch { }
        }

        private static LocalNetworkGamer GetLocalGamer()
        {
            try { return CastleMinerZGame.Instance?.LocalPlayer?.Gamer as LocalNetworkGamer; }
            catch { return null; }
        }

        private static object GetEnemyManager()
        {
            var game = CastleMinerZGame.Instance;
            if (game?.GameScreen == null) return null;
            return game.GameScreen.GetType()
                .GetField("_enemyManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(game.GameScreen);
        }

        private static IEnumerable GetEnemyList()
        {
            var em = GetEnemyManager();
            if (em == null) return null;
            // EnemyManager._enemies is List<BaseZombie>
            return em.GetType()
                .GetField("_enemies", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(em) as IEnumerable;
        }

        private static object GetDragon()
        {
            var em = GetEnemyManager();
            if (em == null) return null;
            // _dragonClient is always set when a dragon exists (works for both host and client)
            var dc = em.GetType().GetField("_dragonClient", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(em);
            if (dc != null) return dc;
            return em.GetType().GetField("_dragon", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(em);
        }

        /// <summary>
        /// Returns every Player object in the current session (local + all remote).
        /// Confirmed from GameScreen.cs: every NetworkGamer's .Tag property holds their Player instance.
        /// </summary>
        private static IEnumerable<Player> GetAllPlayers()
        {
            var session = CastleMinerZGame.Instance?.CurrentNetworkSession;
            if (session == null) yield break;
            foreach (NetworkGamer gamer in session.AllGamers)
            {
                if (gamer?.Tag is Player p) yield return p;
            }
        }

        /// <summary>Returns the NetworkGamer for every remote (non-local) player — used for network messages.</summary>
        private static IEnumerable<NetworkGamer> GetRemoteGamers()
        {
            var session = CastleMinerZGame.Instance?.CurrentNetworkSession;
            if (session == null) yield break;
            foreach (NetworkGamer gamer in session.AllGamers)
            {
                if (gamer != null && !gamer.IsLocal) yield return gamer;
            }
        }

        private static void SetTerrainBlock(Vector3 worldPos, byte blockType)
        {
            // BlockTerrain.Instance.SetBlockAt(int x, int y, int z, int data) — confirmed from source
            try
            {
                var loc = new IntVector3((int)Math.Floor(worldPos.X), (int)Math.Floor(worldPos.Y), (int)Math.Floor(worldPos.Z));
                var type = (BlockTypeEnum)blockType;
                var gamer = GetLocalGamer();
                if (gamer != null)
                {
                    AlterBlockMessage.Send(gamer, loc, type);
                    return;
                }
                BlockTerrain.Instance?.SetBlock(loc, type);
            }
            catch
            {
                try { BlockTerrain.Instance?.SetBlock(IntVector3.FromVector3(worldPos), (BlockTypeEnum)blockType); }
                catch { }
            }
        }

        private static void DeleteTerrainBlock(Vector3 worldPos)
        {
            SetTerrainBlock(worldPos, 0);
        }

        private static object GetSky()
        {
            var game = CastleMinerZGame.Instance;
            if (game?.GameScreen == null) return null;
            // GameScreen._sky is CastleMinerSky — TimeOfDay is a writable float field on it
            return game.GameScreen.GetType()
                .GetField("_sky", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(game.GameScreen);
        }

        private static void SetTimeOfDayLocal(object gameScreen, float timeOfDay)
        {
            // GameScreen.TimeOfDay is read-only, but backing sky.Day is writable via GameScreen.Day.
            // Try the public Day property first, then fallback to private _sky.Day.
            try
            {
                if (gameScreen == null) return;
                timeOfDay -= (float)Math.Floor(timeOfDay);
                float currentDay = 0f;

                var dayProp = gameScreen.GetType().GetProperty("Day",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (dayProp != null && dayProp.CanWrite)
                {
                    try { currentDay = Convert.ToSingle(dayProp.GetValue(gameScreen, null)); } catch { }
                    dayProp.SetValue(gameScreen, (float)Math.Floor(currentDay) + timeOfDay, null);
                    return;
                }

                var sky = GetSky();
                if (sky != null)
                {
                    var f = sky.GetType().GetField("Day",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null && f.FieldType == typeof(float))
                    {
                        try { currentDay = (float)f.GetValue(sky); } catch { }
                        f.SetValue(sky, (float)Math.Floor(currentDay) + timeOfDay);
                        return;
                    }
                }
            }
            catch { }
        }

        private static void SetDayLocal(object gameScreen, float day)
        {
            try
            {
                var f = gameScreen.GetType().GetField("Day",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) { f.SetValue(gameScreen, day); return; }
                var p = gameScreen.GetType().GetProperty("Day",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.CanWrite) p.SetValue(gameScreen, day, null);
            }
            catch { }
        }

        private static float _savedTimeOfDay = -1f;

        private static void SetAmbientLight(float brightness)
        {
            // No direct ambient field in source. Drive brightness via _sky.TimeOfDay:
            // 0.0 = midnight (dark), 0.5 = noon (bright).
            var game = CastleMinerZGame.Instance;
            if (game?.GameScreen == null) return;
            try
            {
                // Save current time so we can restore it later.
                if (_savedTimeOfDay < 0f)
                    _savedTimeOfDay = game.GameScreen.TimeOfDay;

                float target = brightness <= 0f ? 0.0f : (brightness >= 1f ? 0.5f : brightness * 0.5f);
                SetTimeOfDayLocal(game.GameScreen, target);
            }
            catch { }
        }

        private static void RestoreAmbientLight()
        {
            var game = CastleMinerZGame.Instance;
            if (game?.GameScreen == null) return;
            try
            {
                float restore = _savedTimeOfDay >= 0f ? _savedTimeOfDay : 0.5f;
                SetTimeOfDayLocal(game.GameScreen, restore);
            }
            catch { }
            _savedTimeOfDay = -1f;
        }

        private static void SetWaterPlane(bool enable)
        {
            // BlockTerrain.IsWaterWorld is the correct field (confirmed from source)
            try { BlockTerrain.Instance.IsWaterWorld = enable; }
            catch { }
        }

        private static void SetFlying(bool enable)
        {
            // Player.FlyMode is a public property confirmed from source — apply to all players.
            foreach (Player player in GetAllPlayers())
            {
                try { player.FlyMode = enable; }
                catch { }
            }
        }

        private static void SetFOV(float fovDegrees)
        {
            // Player.DefaultFOV is public Angle — apply to all players via reflection (Angle type not imported).
            foreach (Player player in GetAllPlayers())
            {
                try
                {
                    var fovField = player.GetType().GetField("DefaultFOV",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fovField == null) continue;
                    var angleType = fovField.FieldType;
                    var fromDegrees = angleType.GetMethod("FromDegrees",
                        BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(float) }, null);
                    if (fromDegrees == null) continue;
                    object angle = fromDegrees.Invoke(null, new object[] { fovDegrees });
                    fovField.SetValue(player, angle);
                    // Also push to FPSCamera immediately.
                    var fpsCam = player.GetType()
                        .GetProperty("FPSCamera", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.GetValue(player, null);
                    if (fpsCam != null)
                    {
                        fpsCam.GetType()
                            .GetProperty("FieldOfView", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.SetValue(fpsCam, angle, null);
                    }
                }
                catch { }
            }
        }

        private static void RestoreFOV() { SetFOV(73f); } // 73f is the confirmed source default

        private static float _originalSprintSpeed = -1f;
        private static string _sprintFieldName = null;

        private static void SetSprintMultiplier(float mult)
        {
            var player = GetLocalPlayer();
            if (player == null) return;
            try
            {
                string[] candidates = { "_sprintSpeed", "SprintSpeed", "_runSpeed", "RunSpeed",
                                        "_sprintMultiplier", "_moveSpeed", "MoveSpeed", "_walkSpeed", "WalkSpeed" };
                foreach (string name in candidates)
                {
                    var f = player.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f == null || f.FieldType != typeof(float)) continue;
                    if (_originalSprintSpeed < 0f)
                    {
                        _originalSprintSpeed = (float)f.GetValue(player);
                        _sprintFieldName = name;
                    }
                    f.SetValue(player, mult > 0f ? mult : _originalSprintSpeed);
                    return;
                }
            }
            catch { }
        }

        private static void RestoreSprintSpeed()
        {
            float restore = _originalSprintSpeed > 0f ? _originalSprintSpeed : 2f; // 2f confirmed from source
            foreach (Player player in GetAllPlayers())
            {
                try
                {
                    var f = FindSprintField(player);
                    if (f != null) f.SetValue(player, restore);
                }
                catch { }
            }
            _originalSprintSpeed = -1f;
            _sprintFieldName = null;
            _cachedSprintField = null;
        }

        private static void RestoreReach()
        {
            string[] candidates = { "_reach", "Reach", "_blockReach", "BlockReach", "_interactionDistance" };
            foreach (Player player in GetAllPlayers())
            {
                try
                {
                    foreach (string name in candidates)
                    {
                        var f = player.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null && f.FieldType == typeof(float)) { f.SetValue(player, 6f); break; }
                    }
                }
                catch { }
            }
        }

        private static void SetRandomSkyColor()
        {
            // CastleMinerSky has no SkyColor property — vary TimeOfDay to cycle through sky colors
            var game = CastleMinerZGame.Instance;
            if (game?.GameScreen == null) return;
            try { SetTimeOfDayLocal(game.GameScreen, (float)_rng.NextDouble()); }
            catch { }
        }

        private static void DeleteBlocksAroundPlayer(int radius)
        {
            Vector3 playerPos = GetPlayerPosition();
            for (int x = -radius; x <= radius; x++)
            for (int z = -radius; z <= radius; z++)
            for (int y = 1; y <= radius; y++) // skip y=0 so we don't delete below feet
            {
                float dist = (float)Math.Sqrt(x * x + y * y + z * z);
                if (dist > radius) continue;
                DeleteTerrainBlock(new Vector3(
                    (float)Math.Floor(playerPos.X) + x,
                    (float)Math.Floor(playerPos.Y) + y,
                    (float)Math.Floor(playerPos.Z) + z));
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Individual effect implementations
        // ─────────────────────────────────────────────────────────────────────

        private static void EffectKillPlayer()
        {
            var myGamer = CastleMinerZGame.Instance?.MyNetworkGamer;
            // Kill local player via HUD.
            try { InGameHUD.Instance.ApplyDamage(9999f, Vector3.Zero); }
            catch { }
            // Kill remote players via MeleePlayerMessage (confirmed API from source).
            // Send many hits so any weapon value guarantees death regardless of damage per hit.
            if (myGamer != null)
            {
                foreach (NetworkGamer remote in GetRemoteGamers())
                {
                    try
                    {
                        for (int i = 0; i < 50; i++)
                            MeleePlayerMessage.Send(myGamer, remote, (InventoryItemIDs)0, Vector3.Zero);
                    }
                    catch { }
                }
            }
        }

        private static void EffectObliterateEnemies()
        {
            var enemies = GetEnemyList();
            if (enemies == null) { AppendEffectLog("[SKIP] ObliterateEnemies — no enemy list"); return; }
            var gamer = GetLocalGamer();
            if (gamer == null) return;
            byte shooterId = gamer.Id;
            foreach (object enemy in enemies)
            {
                try
                {
                    var takeExplosive = enemy.GetType().GetMethod("TakeExplosiveDamage",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (takeExplosive != null)
                    {
                        var ps = takeExplosive.GetParameters();
                        if (ps.Length == 3)
                            takeExplosive.Invoke(enemy, new object[] { 99999f, shooterId, (short)0 });
                        else
                            takeExplosive.Invoke(enemy, new object[] { 99999f, shooterId, (InventoryItemIDs)0 });
                    }
                    else
                    {
                        // Fallback: TakeDamage
                        var takeDamage = enemy.GetType().GetMethod("TakeDamage",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        takeDamage?.Invoke(enemy, new object[] { Vector3.Zero, Vector3.Zero, null, shooterId });
                    }
                }
                catch { }
            }
        }

        private static void EffectExplodePlayer()
        {
            var game = CastleMinerZGame.Instance;
            if (game?.GameScreen == null) return;
            var explodeMethod = game.GameScreen.GetType().GetMethod("CreateExplosion",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            byte shooterId = game.MyNetworkGamer?.Id ?? 0;
            // Create an explosion at every player's position.
            foreach (Player player in GetAllPlayers())
            {
                try
                {
                    explodeMethod?.Invoke(game.GameScreen,
                        new object[] { player.LocalPosition, 5f, 50f, shooterId, (InventoryItemIDs)0 });
                }
                catch { }
            }
            ApplyDamageToPlayer(50f);
        }

        private static void EffectRandomBlocks()
        {
            Vector3 pos = GetPlayerPosition();
            for (int i = 0; i < 6; i++)
            {
                byte blockType = (byte)_rng.Next(1, 60);
                var offset = new Vector3(
                    _rng.Next(-2, 3),
                    _rng.Next(0, 3),
                    _rng.Next(-2, 3));
                SetTerrainBlock(pos + offset, blockType);
            }
        }

        private static void EffectSpawnRandomEnemy()
        {
            var gamer = GetLocalGamer();
            if (gamer == null) return;
            try
            {
                var game = CastleMinerZGame.Instance;
                if (game?.GameScreen == null) return;
                Vector3 pos = GetPlayerPosition() + new Vector3(2f, 0f, 0f);
                int enemyTypeCount = Enum.GetValues(typeof(EnemyTypeEnum)).Length;
                var enemyType = (EnemyTypeEnum)_rng.Next(0, enemyTypeCount);
                float midnight = game.GameScreen.TimeOfDay < 0.5f ? 0f : 1f;
                int id = _rng.Next(1000, 99999);
                int seed = _rng.Next();
                SpawnEnemyMessage.Send(gamer, pos, enemyType, midnight, id, seed, pos);
            }
            catch { }
        }

        private static void EffectSpawnRandomDragon()
        {
            var gamer = GetLocalGamer();
            if (gamer == null) return;
            try
            {
                int dragonTypeCount = Enum.GetValues(typeof(DragonTypeEnum)).Length;
                var dragonType = (DragonTypeEnum)_rng.Next(0, dragonTypeCount);
                float health = (float)(_rng.NextDouble() * 1000.0 + 20.0);
                SpawnDragonMessage.Send(gamer, 0, dragonType, false, health);
            }
            catch { }
        }

        private static void EffectRandomTeleport()
        {
            float dist = (float)(_rng.NextDouble() * 1000000.0);
            double a = _rng.NextDouble() * Math.PI * 2.0;
            var dest = new Vector3((float)Math.Cos(a) * dist, 80f, (float)Math.Sin(a) * dist);
            TeleportAllPlayers(_ => dest);
        }

        private static void EffectTeleportToStart()
        {
            TeleportAllPlayers(_ => new Vector3(0f, 80f, 0f));
        }

        private static void EffectTeleportToSurface()
        {
            TeleportAllPlayers(p => new Vector3(p.LocalPosition.X, 200f, p.LocalPosition.Z));
        }

        private static void EffectTeleportBiome(float distance)
        {
            TeleportAllPlayers(_ => new Vector3(distance, 80f, 0f));
        }

        private static void EffectGiveRandomItem()
        {
            var ids = (InventoryItemIDs[])Enum.GetValues(typeof(InventoryItemIDs));
            foreach (Player player in GetAllPlayers())
            {
                try
                {
                    var randomId = ids[_rng.Next(0, ids.Length)];
                    int count = _rng.Next(1, 51);
                    var item = InventoryItem.CreateItem(randomId, count);
                    if (item != null) player.PlayerInventory.AddInventoryItem(item, false);
                }
                catch { }
            }
        }

        private static void EffectKillAllEnemies()
        {
            var enemies = GetEnemyList();
            if (enemies == null) return;
            var gamer = GetLocalGamer();
            if (gamer == null) return;
            byte shooterId = gamer.Id;
            foreach (object enemy in enemies)
            {
                try
                {
                    int enemyId = GetIntField(enemy, "EnemyID", "ID", "_enemyID", "_id") ?? 0;
                    int targetId = GetIntField(enemy, "TargetID", "_targetID") ?? 0;
                    KillEnemyMessage.Send(gamer, enemyId, targetId, shooterId, (InventoryItemIDs)0);
                }
                catch { }
            }
        }

        private static void EffectLaunchPlayerUp()
        {
            TeleportAllPlayers(p => new Vector3(p.LocalPosition.X, p.LocalPosition.Y + 40f, p.LocalPosition.Z));
        }

        private static void EffectLaunchAllEnemiesUp()
        {
            var enemies = GetEnemyList();
            if (enemies == null) return;
            foreach (object enemy in enemies)
            {
                try
                {
                    string[] velCandidates = { "_velocity", "Velocity", "_linearVelocity" };
                    foreach (string name in velCandidates)
                    {
                        var f = enemy.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null && f.FieldType == typeof(Vector3))
                        {
                            f.SetValue(enemy, new Vector3(0f, 68f, 0f));
                            break;
                        }
                    }
                }
                catch { }
            }
        }

        private static void EffectTeleportAllEnemiesToPlayer()
        {
            var enemies = GetEnemyList();
            if (enemies == null) return;
            Vector3 playerPos = GetPlayerPosition();
            foreach (object enemy in enemies)
            {
                try
                {
                    string[] posCandidates = { "Position", "_position", "WorldPosition" };
                    foreach (string name in posCandidates)
                    {
                        var f = enemy.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null && f.FieldType == typeof(Vector3)) { f.SetValue(enemy, playerPos); break; }
                        var p = enemy.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null && p.CanWrite) { p.SetValue(enemy, playerPos, null); break; }
                    }
                }
                catch { }
            }
        }

        private static void EffectSetTimeToDay()
        {
            try
            {
                var game = CastleMinerZGame.Instance;
                if (game?.GameScreen == null) return;
                // 0.5 = noon (brightest)
                SetTimeOfDayLocal(game.GameScreen, 0.5f);
            }
            catch { }
        }

        private static void EffectSetTimeToNight()
        {
            try
            {
                var game = CastleMinerZGame.Instance;
                if (game?.GameScreen == null) return;
                // 0.0 = midnight (darkest)
                SetTimeOfDayLocal(game.GameScreen, 0.0f);
            }
            catch { }
        }

        private static void EffectOpenInventory()
        {
            try
            {
                var game = CastleMinerZGame.Instance;
                if (game?.GameScreen == null) return;
                // Toggle the block picker (inventory)
                game.GameScreen.ShowBlockPicker();
            }
            catch { }
        }

        private static void EffectSpawnLiveGrenade()
        {
            var gamer = GetLocalGamer();
            if (gamer == null) return;
            try
            {
                double roll = _rng.NextDouble();
                bool sticky = false;
                bool dud = roll < 0.5;
                if (!dud) sticky = roll > 0.75;

                Vector3 pos = GetPlayerPosition();
                var game = CastleMinerZGame.Instance;
                if (game?.GameScreen == null) return;
                Matrix orientation = game.LocalPlayer?.FPSCamera?.LocalToWorld ?? Matrix.CreateWorld(pos + new Vector3(0f, 1.5f, 0f), Vector3.Forward, Vector3.Up);
                orientation.Translation = pos + orientation.Forward;
                GrenadeMessage.Send(gamer, orientation, sticky ? GrenadeTypeEnum.Sticky : GrenadeTypeEnum.HE, dud ? 5f : 0.25f);
            }
            catch { }
        }

        private static void EffectFireRocket()
        {
            var gamer = GetLocalGamer();
            if (gamer == null) return;
            try
            {
                Vector3 pos = GetPlayerPosition();
                var game = CastleMinerZGame.Instance;
                if (game?.GameScreen == null) return;
                Matrix orientation = game.LocalPlayer?.FPSCamera?.LocalToWorld ?? Matrix.CreateWorld(pos + new Vector3(0f, 1.5f, 0f), Vector3.Forward, Vector3.Up);
                FireRocketMessage.Send(gamer, orientation, InventoryItemIDs.RocketLauncher, false);
            }
            catch { }
        }

        private static void EffectGiveItem(InventoryItemIDs itemId, int count)
        {
            foreach (Player player in GetAllPlayers())
            {
                try
                {
                    var item = InventoryItem.CreateItem(itemId, count);
                    if (item != null) player.PlayerInventory.AddInventoryItem(item, false);
                }
                catch { }
            }
        }

        private static void EffectDeleteBlocks()
        {
            Vector3 pos = GetPlayerPosition();
            int radius = _rng.Next(2, 8);
            for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            for (int z = -radius; z <= radius; z++)
                DeleteTerrainBlock(new Vector3(
                    (float)Math.Floor(pos.X) + x,
                    (float)Math.Floor(pos.Y) + y,
                    (float)Math.Floor(pos.Z) + z));
        }

        private static void EffectSpawnRandomFloor()
        {
            Vector3 pos = GetPlayerPosition();
            float floorY = (float)Math.Floor(pos.Y) - 1f;
            for (int x = -5; x <= 5; x++)
            for (int z = -5; z <= 5; z++)
            {
                byte blockType = (byte)_rng.Next(1, 60);
                SetTerrainBlock(new Vector3(
                    (float)Math.Floor(pos.X) + x,
                    floorY,
                    (float)Math.Floor(pos.Z) + z), blockType);
            }
        }

        private static void EffectQuakeFOV()
        {
            SetFOV(140f);
            ActivateTimed(ChaosEffectID.QuakeFOV);
        }

        private static void EffectRestartGame()
        {
            try
            {
                var game = CastleMinerZGame.Instance;
                if (game == null) return;
                // Try common restart methods.
                string[] candidates = { "Restart", "RestartGame", "ResetGame", "StartNewGame" };
                foreach (string name in candidates)
                {
                    var m = game.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m != null) { m.Invoke(game, null); return; }
                }
                if (game.GameScreen != null)
                {
                    foreach (string name in candidates)
                    {
                        var m = game.GameScreen.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (m != null) { m.Invoke(game.GameScreen, null); return; }
                    }
                }
            }
            catch { }
        }

        private static void EffectLava()
        {
            ApplyDamageToPlayer(25f);
            // Play burn sound via reflection.
            try
            {
                var game = CastleMinerZGame.Instance;
                string[] candidates = { "PlayBurnSound", "PlayLavaSound", "PlayDamageSound" };
                foreach (string name in candidates)
                {
                    var m = game?.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m != null) { m.Invoke(game, null); return; }
                }
            }
            catch { }
        }

        private static void EffectFlying()
        {
            SetFlying(true);
            ActivateTimed(ChaosEffectID.Flying);
        }

        private static void EffectDarkMode()
        {
            SetAmbientLight(0f);
            ActivateTimed(ChaosEffectID.DarkMode);
        }

        private static void EffectFullbright()
        {
            SetAmbientLight(1f);
            ActivateTimed(ChaosEffectID.Fullbright);
        }

        private static void EffectSwitchCurrentTray()
        {
            try
            {
                var inv = CastleMinerZGame.Instance?.LocalPlayer?.PlayerInventory;
                if (inv == null) return;
                var tray = inv.TrayManager;
                var traySwitch = tray?.GetType().GetMethod("SwitchCurrentTray", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (traySwitch != null) { traySwitch.Invoke(tray, null); return; }
                string[] candidates = { "SwitchCurrentTray", "SwitchTray", "ToggleTray", "NextTray", "SwitchActiveTray" };
                foreach (string name in candidates)
                {
                    var m = inv.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m != null) { m.Invoke(inv, null); return; }
                }
                // Try toggling a boolean tray field.
                string[] trayFields = { "_activeTray", "ActiveTray", "_currentTray", "_trayIndex" };
                foreach (string name in trayFields)
                {
                    var f = inv.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null && f.FieldType == typeof(bool))
                    {
                        f.SetValue(inv, !(bool)f.GetValue(inv));
                        return;
                    }
                    if (f != null && f.FieldType == typeof(int))
                    {
                        int cur = (int)f.GetValue(inv);
                        f.SetValue(inv, cur == 0 ? 1 : 0);
                        return;
                    }
                }
            }
            catch { }
        }

        private static void EffectCorruptedAssaultRifle()
        {
            try
            {
                var inv = CastleMinerZGame.Instance?.LocalPlayer?.PlayerInventory;
                if (inv == null) return;
                var rifle = InventoryItem.CreateItem(InventoryItemIDs.AssultRifle, 1);
                if (rifle != null)
                    inv.TrayManager.SetItem(inv.SelectedInventoryIndex, rifle);
                var ammo = InventoryItem.CreateItem(InventoryItemIDs.Bullets, 500);
                if (ammo != null)
                    inv.AddInventoryItem(ammo, false);
            }
            catch { }
        }

        private static void EffectAttachBlocksToEnemies()
        {
            var enemies = GetEnemyList();
            if (enemies == null) return;
            foreach (object enemy in enemies)
            {
                try
                {
                    Vector3 enemyPos = Vector3.Zero;
                    string[] posCandidates = { "Position", "_position", "WorldPosition" };
                    foreach (string name in posCandidates)
                    {
                        var f = enemy.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null && f.FieldType == typeof(Vector3)) { enemyPos = (Vector3)f.GetValue(enemy); break; }
                        var p = enemy.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null) { enemyPos = (Vector3)p.GetValue(enemy, null); break; }
                    }
                    if (enemyPos != Vector3.Zero)
                    {
                        byte blockType = (byte)_rng.Next(1, 60);
                        SetTerrainBlock(enemyPos, blockType);
                    }
                }
                catch { }
            }
        }

        private static void EffectAllEnemiesGiveUp()
        {
            var enemies = GetEnemyList();
            if (enemies == null) return;
            foreach (object enemy in enemies)
            {
                try
                {
                    int enemyId = GetIntField(enemy, "EnemyID", "ID", "_enemyID", "_id") ?? 0;
                    int targetId = GetIntField(enemy, "TargetID", "_targetID") ?? 0;
                    EnemyGiveUpMessage.Send(enemyId, targetId);
                }
                catch { }
            }
        }

        private static void EffectAllEnemiesSpeedUp()
        {
            var enemies = GetEnemyList();
            if (enemies == null) return;
            var gamer = GetLocalGamer();
            if (gamer == null) return;
            foreach (object enemy in enemies)
            {
                try
                {
                    int enemyId = GetIntField(enemy, "EnemyID", "ID", "_enemyID", "_id") ?? 0;
                    int targetId = GetIntField(enemy, "TargetID", "_targetID") ?? 0;
                    SpeedUpEnemyMessage.Send(gamer, enemyId, targetId);
                }
                catch { }
            }
        }

        private static void EffectAttackOnTitan()
        {
            var enemies = GetEnemyList();
            if (enemies == null) return;
            foreach (object enemy in enemies)
            {
                try
                {
                    string[] scaleCandidates = { "Scale", "_scale", "ModelScale", "_modelScale" };
                    foreach (string name in scaleCandidates)
                    {
                        var f = enemy.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null && f.FieldType == typeof(float)) { f.SetValue(enemy, 10f); break; }
                        var p = enemy.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null && p.CanWrite) { p.SetValue(enemy, 10f, null); break; }
                    }
                }
                catch { }
            }
        }

        private static void EffectTeleportPlayerToDragon()
        {
            object dragon = GetDragon();
            if (dragon == null)
            {
                AppendEffectLog("[SKIP] TeleportPlayerToDragon — no dragon active");
                return;
            }
            try
            {
                Vector3 dragonPos = Vector3.Zero;
                string[] posCandidates = { "Position", "LocalPosition", "WorldPosition", "_position", "_localPosition", "_worldPosition" };
                var t = dragon.GetType();
                bool found = false;
                while (t != null && !found)
                {
                    foreach (string name in posCandidates)
                    {
                        var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null && f.FieldType == typeof(Vector3)) { dragonPos = (Vector3)f.GetValue(dragon); found = true; break; }
                        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null && p.PropertyType == typeof(Vector3)) { dragonPos = (Vector3)p.GetValue(dragon, null); found = true; break; }
                    }
                    t = t.BaseType;
                }
                if (!found || dragonPos == Vector3.Zero)
                {
                    AppendEffectLog("[SKIP] TeleportPlayerToDragon — could not read dragon position");
                    return;
                }
                TeleportAllPlayers(_ => dragonPos);
            }
            catch { }
        }

        private static void EffectReach()
        {
            string[] candidates = { "_reach", "Reach", "_blockReach", "BlockReach", "_interactionDistance" };
            foreach (Player player in GetAllPlayers())
            {
                try
                {
                    foreach (string name in candidates)
                    {
                        var f = player.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null && f.FieldType == typeof(float)) { f.SetValue(player, 9999f); break; }
                    }
                }
                catch { }
            }
            ActivateTimed(ChaosEffectID.Reach);
        }

        private static void EffectNoUWalls()
        {
            Vector3 pos = GetPlayerPosition();
            const byte bedrock = 1; // BlockTypeEnum.Rock/Bedrock - index 1 is typically bedrock
            float cx = (float)Math.Floor(pos.X);
            float cy = (float)Math.Floor(pos.Y);
            float cz = (float)Math.Floor(pos.Z);
            int size = 5;
            // North/South walls.
            for (int x = -size; x <= size; x++)
            for (int y = 0; y < 5; y++)
            {
                SetTerrainBlock(new Vector3(cx + x, cy + y, cz - size), bedrock);
                SetTerrainBlock(new Vector3(cx + x, cy + y, cz + size), bedrock);
            }
            // East/West walls.
            for (int z = -size; z <= size; z++)
            for (int y = 0; y < 5; y++)
            {
                SetTerrainBlock(new Vector3(cx - size, cy + y, cz + z), bedrock);
                SetTerrainBlock(new Vector3(cx + size, cy + y, cz + z), bedrock);
            }
        }

        private static void EffectNoSprint()
        {
            // Player._sprintMultiplier default = 2f (from source). Set to 0.1 so "sprint" is slower than walk.
            SetSprintField(0.1f);
            ActivateTimed(ChaosEffectID.NoSprint);
        }

        private static void EffectSuperSprint()
        {
            // Set _sprintMultiplier to 10x to get rocket-fast sprint.
            SetSprintField(10f);
            ActivateTimed(ChaosEffectID.SuperSprint);
        }

        private static FieldInfo _cachedSprintField = null;

        private static FieldInfo FindSprintField(object player)
        {
            if (_cachedSprintField != null) return _cachedSprintField;
            string[] candidates = { "_sprintMultiplier", "SprintMultiplier", "_sprintSpeed", "SprintSpeed",
                                    "_runSpeed", "RunSpeed", "_moveSpeed", "MoveSpeed",
                                    "_sprintFactor", "sprintMultiplier", "_runSpeedMult" };
            var t = player.GetType();
            while (t != null)
            {
                foreach (string name in candidates)
                {
                    var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null && f.FieldType == typeof(float)) { _cachedSprintField = f; return f; }
                }
                t = t.BaseType;
            }
            return null;
        }

        private static void SetSprintField(float value)
        {
            foreach (Player player in GetAllPlayers())
            {
                try
                {
                    var f = FindSprintField(player);
                    if (f == null) continue;
                    if (_originalSprintSpeed < 0f)
                    {
                        _originalSprintSpeed = (float)f.GetValue(player);
                        _sprintFieldName = f.Name;
                    }
                    f.SetValue(player, value);
                }
                catch { }
            }
        }

        private static void EffectSetRandomDay()
        {
            try
            {
                var game = CastleMinerZGame.Instance;
                if (game?.GameScreen == null) return;
                // Save current day to restore later
                var dayProp = game.GameScreen.GetType().GetProperty("Day",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (dayProp != null && dayProp.CanRead)
                    _savedDay = (float)dayProp.GetValue(game.GameScreen, null);
                // Set random day (0-1000000 range)
                float randomDay = (float)_rng.Next(0, 1000001);
                SetDayLocal(game.GameScreen, randomDay);
                // Reset time to noon
                SetTimeOfDayLocal(game.GameScreen, 0.5f);
            }
            catch { }
        }

        private static void RestoreDay()
        {
            if (_savedDay < 0f) return;
            try
            {
                var game = CastleMinerZGame.Instance;
                if (game?.GameScreen == null) return;
                SetDayLocal(game.GameScreen, _savedDay);
                _savedDay = -1f;
            }
            catch { }
        }

        private static void EffectDropAllItems()
        {
            // PlayerInventory.DropAll(bool dropTray) confirmed from source — apply to every player.
            foreach (Player player in GetAllPlayers())
                try { player.PlayerInventory.DropAll(true); }
                catch { }
        }

        private static void EffectLightning()
        {
            try
            {
                Vector3 pos = GetPlayerPosition();
                var game = CastleMinerZGame.Instance;
                if (game?.GameScreen == null) return;
                string[] candidates = { "SpawnLightning", "CreateLightning", "StrikeLightning", "Lightning" };
                foreach (string name in candidates)
                {
                    var m = game.GameScreen.GetType().GetMethod(name,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m != null) { m.Invoke(game.GameScreen, new object[] { pos }); return; }
                }
            }
            catch { }
        }

        private static void EffectKillDragon()
        {
            object dragon = GetDragon();
            if (dragon == null) { AppendEffectLog("[SKIP] KillDragon — no dragon active"); return; }
            var gamer = GetLocalGamer();
            if (gamer == null) return;
            try
            {
                Vector3 pos = Vector3.Zero;
                string[] posCandidates = { "Position", "_position" };
                foreach (string name in posCandidates)
                {
                    var f = dragon.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null && f.FieldType == typeof(Vector3)) { pos = (Vector3)f.GetValue(dragon); break; }
                }
                byte shooterId = gamer.Id;
                KillDragonMessage.Send(gamer, pos, shooterId, (InventoryItemIDs)0);
            }
            catch { }
        }

        private static void EffectDisco()
        {
            ChaosState.DiscoSubTimer = 0f;
            ActivateTimed(ChaosEffectID.Disco);
        }

        private static void EffectWater()
        {
            SetWaterPlane(true);
            ActivateTimed(ChaosEffectID.Water);
        }

        private static void EffectForceField()
        {
            ChaosState.ForceFieldSubTimer = 0f;
            ActivateTimed(ChaosEffectID.ForceField);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Utility
        // ─────────────────────────────────────────────────────────────────────

        private static int? GetIntField(object obj, params string[] names)
        {
            foreach (string name in names)
            {
                try
                {
                    var f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
                    var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(obj, null);
                }
                catch { }
            }
            return null;
        }

        private static void ApplyDamageToPlayer(float damage)
        {
            try { InGameHUD.Instance.ApplyDamage(damage, Vector3.Zero); }
            catch { }
        }

        private static object GetInventoryItemNone()
        {
            try
            {
                var field = typeof(InventoryItemIDs).GetField("None", BindingFlags.Public | BindingFlags.Static);
                if (field != null) return field.GetValue(null);
            }
            catch { }
            return 0;
        }

        private static object GetInventoryItemByName(string name)
        {
            try
            {
                var field = typeof(InventoryItemIDs).GetField(name, BindingFlags.Public | BindingFlags.Static);
                if (field != null) return field.GetValue(null);
            }
            catch { }
            return 0;
        }

        private static void EffectGiveItemByName(string itemName, int count)
        {
            if (!Enum.TryParse(itemName, out InventoryItemIDs id)) return;
            foreach (Player player in GetAllPlayers())
            {
                try
                {
                    var item = InventoryItem.CreateItem(id, count);
                    if (item != null) player.PlayerInventory.AddInventoryItem(item, false);
                }
                catch { }
            }
        }

        private static string GetEffectName(ChaosEffectID id)
        {
            switch (id)
            {
                case ChaosEffectID.KillPlayer:                   return "Kill Player";
                case ChaosEffectID.ObliterateEnemies:            return "Obliterate Enemies";
                case ChaosEffectID.ExplodePlayer:                return "Explode Player";
                case ChaosEffectID.RandomBlocks:                 return "Random Blocks";
                case ChaosEffectID.SpawnRandomEnemy:             return "Spawn Random Enemy";
                case ChaosEffectID.SpawnRandomDragon:            return "Spawn Random Dragon";
                case ChaosEffectID.RandomTeleport:               return "Random Teleport";
                case ChaosEffectID.TeleportToStart:              return "Teleport To Start";
                case ChaosEffectID.TeleportToSurface:            return "Teleport To Surface";
                case ChaosEffectID.TeleportToLagoon:             return "Teleport To Lagoon";
                case ChaosEffectID.TeleportToDesert:             return "Teleport To Desert";
                case ChaosEffectID.TeleportToMountains:          return "Teleport To Mountains";
                case ChaosEffectID.TeleportToArctic:             return "Teleport To Arctic";
                case ChaosEffectID.TeleportToMinersCove:         return "Teleport To Miners Cove";
                case ChaosEffectID.TeleportToHellOnEarth:        return "Teleport To Hell On Earth";
                case ChaosEffectID.AroundTheWorld:               return "Around The World";
                case ChaosEffectID.GiveRandomItem:               return "Give Random Item";
                case ChaosEffectID.KillAllEnemies:               return "Kill All Enemies";
                case ChaosEffectID.LaunchPlayerUp:               return "Launch Player Up";
                case ChaosEffectID.LaunchAllEnemiesUp:           return "Launch All Enemies Up";
                case ChaosEffectID.TeleportAllEnemiesToPlayer:   return "Teleport All Enemies To Player";
                case ChaosEffectID.SetTimeToDay:                 return "Set Time To Day";
                case ChaosEffectID.SetTimeToNight:               return "Set Time To Night";
                case ChaosEffectID.Nothing:                      return "Nothing (:";
                case ChaosEffectID.OpenInventory:                return "Open Inventory";
                case ChaosEffectID.SpawnLiveGrenade:             return "Spawn Live Grenade";
                case ChaosEffectID.FireRocket:                   return "Fire Rocket";
                case ChaosEffectID.GiveHauntedChainsaw:          return "Give Haunted Chainsaw";
                case ChaosEffectID.GiveDiamondDoor:              return "Give Diamond Door";
                case ChaosEffectID.GiveTechDoor:                 return "Give Tech Door";
                case ChaosEffectID.DeleteBlocks:                 return "Delete Blocks";
                case ChaosEffectID.SpawnRandomFloorOfBlocks:     return "Spawn Random Floor Of Blocks";
                case ChaosEffectID.GodMode:                      return "God Mode";
                case ChaosEffectID.QuakeFOV:                     return "Quake FOV";
                case ChaosEffectID.AirJump:                      return "Air Jump";
                case ChaosEffectID.NoJump:                       return "No Jump";
                case ChaosEffectID.ExplosiveAmmo:                return "Explosive Ammo";
                case ChaosEffectID.RestartGame:                  return "Restart Game";
                case ChaosEffectID.Lava:                         return "LAVA!!";
                case ChaosEffectID.LaunchEveryoneUp:             return "Launch Everyone Up";
                case ChaosEffectID.Flying:                       return "Flying";
                case ChaosEffectID.Timelapse:                    return "Timelapse";
                case ChaosEffectID.NoHUD:                        return "No HUD";
                case ChaosEffectID.Bloom:                        return "BLOOM";
                case ChaosEffectID.Bind:                         return "Bind";
                case ChaosEffectID.DarkMode:                     return "Dark Mode";
                case ChaosEffectID.SwitchCurrentTray:            return "Switch Current Tray";
                case ChaosEffectID.CorruptedAssaultRifle:        return "Corrupted Assault Rifle";
                case ChaosEffectID.AttachRandomBlocksToAllEnemies: return "Attach Random Blocks To All Enemies";
                case ChaosEffectID.AllEnemiesGiveUp:             return "All Enemies Give Up";
                case ChaosEffectID.AllEnemiesSpeedUp:            return "All Enemies Speed Up";
                case ChaosEffectID.AttackOnTitan:                return "Attack On Titan";
                case ChaosEffectID.TeleportPlayerToDragon:       return "Teleport Player To Dragon";
                case ChaosEffectID.Fullbright:                   return "Fullbright";
                case ChaosEffectID.Reach:                        return "Reach";
                case ChaosEffectID.InfiniteAmmo:                 return "Infinite Ammo";
                case ChaosEffectID.NoRecoil:                     return "No Recoil";
                case ChaosEffectID.FastShot:                     return "Fast Shot";
                case ChaosEffectID.RapidMine:                    return "Rapid Mine";
                case ChaosEffectID.RapidKnife:                   return "Rapid Knife";
                case ChaosEffectID.RapidPlace:                   return "Rapid Place";
                case ChaosEffectID.BlockBrush:                   return "Block Brush";
                case ChaosEffectID.NoUWalls:                     return "NoUWalls";
                case ChaosEffectID.NoSprint:                     return "No Sprint";
                case ChaosEffectID.SuperSprint:                  return "Super Sprint";
                case ChaosEffectID.SetRandomDay:                 return "Set Random Day";
                case ChaosEffectID.BrokenExplosiveAmmo:          return "Broken Explosive Ammo";
                case ChaosEffectID.BrokenGun:                    return "Broken Gun";
                case ChaosEffectID.DropAllItems:                 return "Drop All Items";
                case ChaosEffectID.Lightning:                    return "Lightning";
                case ChaosEffectID.KillDragon:                   return "Kill Dragon";
                case ChaosEffectID.EnemyNameTags:                return "Enemy Name Tags";
                case ChaosEffectID.DragonNameTags:               return "Dragon Name Tags";
                case ChaosEffectID.Disco:                        return "Disco";
                case ChaosEffectID.Water:                        return "Water";
                case ChaosEffectID.CantDig:                      return "Cant Dig";
                case ChaosEffectID.CantBuild:                    return "Cant Build";
                case ChaosEffectID.CantCraft:                    return "Cant Craft";
                case ChaosEffectID.CantOpenInventory:            return "Cant Open Inventory";
                case ChaosEffectID.ForceField:                   return "Force Field";
                case ChaosEffectID.CantSwitchTrays:              return "Cant Switch Trays";
                default:                                          return id.ToString();
            }
        }
    }
}
