using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using DNA.CastleMinerZ;
using DNA.CastleMinerZ.AI;
using DNA.CastleMinerZ.Inventory;
using DNA.CastleMinerZ.Net;
using DNA.CastleMinerZ.UI;
using DNA.Drawing.UI;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using static ModLoader.LogSystem;

namespace ChaosMod
{
    internal static class GamePatches
    {
        private static Harmony _harmony;
        private const string HarmonyId = "castleminerz.mods.chaosmod.patches";

        public static void ApplyAllPatches()
        {
            _harmony = new Harmony(HarmonyId);
            // Apply each patch class individually so one missing method doesn't kill the whole mod.
            foreach (var type in typeof(GamePatches).GetNestedTypes(
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                try
                {
                    new HarmonyLib.PatchClassProcessor(_harmony, type).Patch();
                }
                catch (Exception ex)
                {
                    Log($"[ChaosMod] Skipped patch {type.Name}: {ex.Message}");
                }
            }
        }

        public static void DisableAll()
        {
            _harmony?.UnpatchAll(HarmonyId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // HUD rendering: notification banner + visual effects
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(InGameHUD), "Draw")]
        private static class Patch_NoHUD
        {
            private static bool Prefix()
            {
                if (ChaosState.NoHUD) return false; // Skip entire HUD draw
                return true;
            }
        }

        [HarmonyPatch(typeof(InGameHUD), nameof(InGameHUD.DrawDistanceStr))]
        private static class Patch_DrawNotification
        {
            private static readonly FieldInfo _medFontField =
                AccessTools.Field(typeof(CastleMinerZGame), "_medFont");

            private static void Postfix(SpriteBatch spriteBatch)
            {
                try
                {
                    var game = CastleMinerZGame.Instance;
                    if (spriteBatch == null)
                        return;

                    if (!(_medFontField?.GetValue(game) is SpriteFont font)) return;

                    var rect   = Screen.Adjuster.ScreenRect;
                    var scale  = Screen.Adjuster.ScaleFactor.Y;
                    var center = new Vector2(rect.Center.X, rect.Center.Y);

                    // ── Chaos chat-style message queue ───────────────────────
                    DrawChatLines(spriteBatch, font, scale, rect);

                    // ── Bind (blindness) overlay ──────────────────────────────
                    if (ChaosState.Bind)
                    {
                        // Draw a full-screen black rectangle.
                        DrawFilledRect(spriteBatch, rect, Color.Black);
                    }

                    // ── No HUD: skip HUD-layer text but still draw overlays ───
                    // (This patch runs after the normal HUD so we can't truly hide it here.
                    //  The NoHUD flag is used by the separate InGameHUD.Draw prefix below.)

                    // ── BLOOM overlay ─────────────────────────────────────────
                    if (ChaosState.Bloom)
                    {
                        var bloomColor = new Color(255, 255, 255, 120);
                        DrawFilledRect(spriteBatch, rect, bloomColor);
                    }

                    // ── Enemy name tags ───────────────────────────────────────
                    if (ChaosState.EnemyNameTags)
                        DrawEnemyNameTags(spriteBatch, font, scale, game);

                    // ── Dragon name tag ───────────────────────────────────────
                    if (ChaosState.DragonNameTags)
                        DrawDragonNameTag(spriteBatch, font, scale, game);

                }
                catch { }
            }

            private static void DrawChatLines(SpriteBatch sb, SpriteFont font, float scale, Rectangle rect)
            {
                try
                {
                    ChaosEffects.ChatLine[] lines;
                    lock (ChaosEffects.ChatLock)
                        lines = ChaosEffects.ChatQueue.ToArray();

                    if (lines.Length == 0) return;

                    float textScale = scale * 0.9f;
                    float lineH     = font.LineSpacing * textScale;

                    // Anchor: center-right, vertically centered around 40% down the screen
                    float blockHeight = lineH * lines.Length;
                    float startY = rect.Center.Y - blockHeight / 2f;
                    // Measure widest line so we right-align against right edge with a margin
                    float margin = 14f * scale;

                    for (int i = 0; i < lines.Length; i++)
                    {
                        string text  = lines[i].Text;
                        float  alpha = Math.Min(1f, lines[i].TimeLeft);
                        Color  col    = lines[i].Color * alpha;
                        Color  shadow = Color.Black * (alpha * 0.8f);

                        float textW = font.MeasureString(text).X * textScale;
                        float x     = rect.Right - textW - margin;
                        var   pos   = new Vector2(x, startY + i * lineH);

                        sb.DrawString(font, text, pos + new Vector2(1, 1), shadow, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
                        sb.DrawString(font, text, pos,                      col,    0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
                    }
                }
                catch { }
            }

            private static void DrawEnemyNameTags(SpriteBatch sb, SpriteFont font, float scale, CastleMinerZGame game)
            {
                try
                {
                    // Iterate enemies and project their positions to screen space.
                    // Without a proper WorldToScreen helper we draw names at a fixed HUD offset per enemy index.
                    var enemies = GetEnemyList(game);
                    if (enemies == null) return;
                    int idx = 0;
                    foreach (object enemy in enemies)
                    {
                        try
                        {
                            string name = enemy.GetType().Name;
                            var rect = Screen.Adjuster.ScreenRect;
                            var pos = new Vector2(rect.Right - 180f * scale, rect.Top + (30f + idx * 20f) * scale);
                            sb.DrawString(font, name, pos, Color.LimeGreen, 0f, Vector2.Zero, scale * 0.8f, SpriteEffects.None, 0f);
                            idx++;
                            if (idx > 20) break;
                        }
                        catch { }
                    }
                }
                catch { }
            }

            private static void DrawDragonNameTag(SpriteBatch sb, SpriteFont font, float scale, CastleMinerZGame game)
            {
                try
                {
                    object dragon = GetDragon(game);
                    if (dragon == null) return;
                    string name = dragon.GetType().Name;
                    var rect = Screen.Adjuster.ScreenRect;
                    var pos = new Vector2(rect.Right - 200f * scale, rect.Top + 20f * scale);
                    sb.DrawString(font, "Dragon: " + name, pos, Color.OrangeRed, 0f, Vector2.Zero, scale * 0.9f, SpriteEffects.None, 0f);
                }
                catch { }
            }

            private static System.Collections.IEnumerable GetEnemyList(CastleMinerZGame game)
            {
                if (game?.GameScreen == null) return null;
                var gs = game.GameScreen;
                string[] candidates = { "_enemies", "Enemies", "_enemyList", "EnemyList", "_spawnedEnemies" };
                foreach (string name in candidates)
                {
                    try
                    {
                        var f = gs.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f?.GetValue(gs) is System.Collections.IEnumerable list) return list;
                        var p = gs.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p?.GetValue(gs, null) is System.Collections.IEnumerable plist) return plist;
                    }
                    catch { }
                }
                return null;
            }

            private static object GetDragon(CastleMinerZGame game)
            {
                if (game?.GameScreen == null) return null;
                var gs = game.GameScreen;
                string[] candidates = { "_dragon", "Dragon", "_currentDragon", "CurrentDragon", "DragonEntity" };
                foreach (string name in candidates)
                {
                    try
                    {
                        var f = gs.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null) { object val = f.GetValue(gs); if (val != null) return val; }
                        var p = gs.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null) { object val = p.GetValue(gs, null); if (val != null) return val; }
                    }
                    catch { }
                }
                return null;
            }

            private static Texture2D _pixelTex;

            private static Texture2D GetPixel(GraphicsDevice gd)
            {
                if (_pixelTex != null) return _pixelTex;
                _pixelTex = new Texture2D(gd, 1, 1);
                _pixelTex.SetData(new[] { Color.White });
                return _pixelTex;
            }

            private static void DrawFilledRect(SpriteBatch sb, Rectangle rect, Color color)
            {
                try
                {
                    var gd = CastleMinerZGame.Instance?.GraphicsDevice;
                    if (gd == null) return;
                    var tex = GetPixel(gd);
                    sb.Draw(tex, rect, color);
                }
                catch { }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // God Mode: skip all damage to player
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(InGameHUD), nameof(InGameHUD.ApplyDamage))]
        private static class Patch_GodMode
        {
            private static bool Prefix()
            {
                if (ChaosState.GodMode) return false; // skip damage
                return true;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // BaseZombie patches: Explosive Ammo, Broken Explosive Ammo, Broken Gun
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(BaseZombie), "TakeDamage")]
        private static class Patch_ExplosiveAmmo_ZombieDamage
        {
            private static void Prefix(Vector3 damagePosition, byte shooterID)
            {
                if (!ChaosState.ExplosiveAmmo) return;
                try
                {
                    var game = CastleMinerZGame.Instance;
                    if (game == null || !game.IsLocalPlayerId(shooterID)) return;
                    SpawnExplosionAt(damagePosition);
                }
                catch { }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Gun fire patches: Broken Gun, Broken Explosive Ammo, Infinite Ammo,
        //                   No Recoil, Fast Shot
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(GunInventoryItem), "ProcessInput")]
        private static class Patch_GunFire
        {
            private static bool Prefix()
            {
                // Broken Gun: skip firing entirely.
                if (ChaosState.BrokenGun) return false;
                return true;
            }

            private static void Postfix(GunInventoryItem __instance)
            {
                try
                {
                    if (ChaosState.BrokenExplosiveAmmo)
                    {
                        // Spawn explosion at player position when firing.
                        var pos = GetPlayerPosition();
                        SpawnExplosionAt(pos);
                        ApplyDamageToPlayer(5f);
                    }

                    if (ChaosState.InfiniteAmmo)
                    {
                        // Restore ammo after firing.
                        var ammoField = __instance.GetType().GetField("_ammo",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            ?? __instance.GetType().GetField("Ammo",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (ammoField != null && ammoField.FieldType == typeof(int))
                        {
                            int max = GetIntFieldValue(__instance, "_maxAmmo", "MaxAmmo", "MaxBullets");
                            if (max <= 0) max = 30;
                            ammoField.SetValue(__instance, max);
                        }
                    }

                    if (ChaosState.NoRecoil)
                    {
                        // Zero out recoil field.
                        string[] recoilCandidates = { "_recoil", "Recoil", "_recoilAmount", "_currentRecoil" };
                        foreach (string name in recoilCandidates)
                        {
                            var f = __instance.GetType().GetField(name,
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (f != null && f.FieldType == typeof(float)) { f.SetValue(__instance, 0f); break; }
                        }
                    }

                    if (ChaosState.FastShot)
                    {
                        // Zero out fire delay so gun fires as fast as possible.
                        string[] delayCandidates = { "_fireDelay", "FireDelay", "_fireTimer", "_shootTimer", "_cooldown" };
                        foreach (string name in delayCandidates)
                        {
                            var f = __instance.GetType().GetField(name,
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (f != null && f.FieldType == typeof(float)) { f.SetValue(__instance, 0f); break; }
                        }
                    }
                }
                catch { }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pickaxe/Knife/Place: Rapid Mine, Rapid Knife, Rapid Place,
        //                      Cant Dig, Cant Build, Cant Craft, Block Brush
        // ─────────────────────────────────────────────────────────────────────

        // SpadeInventoryItem has no Use/ProcessInput override — only TimeToDig().
        // Patch that to block or speed up digging.
        [HarmonyPatch(typeof(SpadeInventoryItem), "TimeToDig")]
        private static class Patch_SpadeTimeToDig
        {
            private static void Postfix(ref float __result)
            {
                if (ChaosState.CantDig)        __result = 999999f;
                else if (ChaosState.RapidMine) __result = 0f;
            }
        }

        // BlockInventoryItem.ProcessInput calls hud.Build() — patch it for CantBuild / RapidPlace.
        [HarmonyPatch(typeof(BlockInventoryItem), "ProcessInput")]
        private static class Patch_BlockPlace
        {
            private static bool Prefix()
            {
                if (ChaosState.CantBuild) return false;
                return true;
            }

            private static void Postfix(BlockInventoryItem __instance)
            {
                if (!ChaosState.RapidPlace) return;
                try
                {
                    string[] delayCandidates = { "_placeDelay", "_buildDelay", "_cooldown", "_useDelay", "_timer", "_delay" };
                    foreach (string name in delayCandidates)
                    {
                        var f = __instance.GetType().GetField(name,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null && f.FieldType == typeof(float)) { f.SetValue(__instance, 0f); break; }
                    }
                }
                catch { }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Player: NoJump / AirJump
        // DNA.CastleMinerZ.Player.Jump() is public override — patch directly.
        // CanJump is a protected property — Postfix it to force true (AirJump) or false (NoJump).
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(Player), nameof(Player.Jump))]
        private static class Patch_PlayerJump
        {
            private static bool Prefix()
            {
                if (ChaosState.NoJump) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "CanJump", MethodType.Getter)]
        private static class Patch_PlayerCanJump
        {
            private static void Postfix(ref bool __result)
            {
                if (ChaosState.AirJump) __result = true;
                if (ChaosState.NoJump)  __result = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Inventory open/close and tray switch guards
        // ─────────────────────────────────────────────────────────────────────

        // These are applied dynamically since we don't have strongly-typed method refs.

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static void SpawnExplosionAt(Vector3 pos)
        {
            try
            {
                var game = CastleMinerZGame.Instance;
                if (game?.GameScreen == null) return;
                var m = game.GameScreen.GetType().GetMethod("CreateExplosion",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (m != null)
                {
                    byte shooterId = 0;
                    try
                    {
                        if (game.LocalPlayer?.Gamer is DNA.Net.GamerServices.LocalNetworkGamer gamer)
                            shooterId = gamer.Id;
                    }
                    catch { }
                    m.Invoke(game.GameScreen, new object[] { pos, 3f, 30f, shooterId, (InventoryItemIDs)0 });
                }
            }
            catch { }
        }

        private static Vector3 GetPlayerPosition()
        {
            var player = CastleMinerZGame.Instance?.LocalPlayer;
            if (player == null) return Vector3.Zero;
            try
            {
                var prop = player.GetType().GetProperty("Position",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null) return (Vector3)prop.GetValue(player, null);
                var field = player.GetType().GetField("Position",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) return (Vector3)field.GetValue(player);
            }
            catch { }
            return Vector3.Zero;
        }

        private static int GetIntFieldValue(object obj, params string[] names)
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
            return 0;
        }

        private static void ApplyDamageToPlayer(float damage)
        {
            try { InGameHUD.Instance.ApplyDamage(damage, Vector3.Zero); }
            catch { }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GameModeMenu: inject "Chaos" mode item + intercept selection
        // ─────────────────────────────────────────────────────────────────────

        // Sentinel tag — falls through the game's switch (default: return) harmlessly.
        // We intercept it in TryPatchGameModeSelection below.
        private static readonly GameModeTypes ChaosModeTag = (GameModeTypes)99;

        [HarmonyPatch]
        private static class Patch_GameModeMenu_InjectChaos
        {
            // Track which menu instances we have already injected into by identity hash code.
            // This is bullet-proof: RuntimeHelpers.GetHashCode is based on object identity, not value.
            private static readonly HashSet<int> _injectedInstances = new HashSet<int>();

            private static MethodBase TargetMethod()
            {
                var asm = typeof(CastleMinerZGame).Assembly;
                var t = asm.GetType("DNA.CastleMinerZ.UI.GameModeMenu");
                if (t == null) { Log("[ChaosMod] GameModeMenu type not found"); return null; }
                // Prefer OnActivated (fires once on show) — fall back to OnUpdate if missing.
                return AccessTools.Method(t, "OnActivated")
                    ?? AccessTools.Method(t, "OnUpdate");
            }

            private static void Postfix(object __instance)
            {
                try
                {
                    if (__instance == null) return;

                    // Check by object identity — if we already injected into THIS instance, stop.
                    int id = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(__instance);
                    if (_injectedInstances.Contains(id)) return;

                    var t = __instance.GetType();

                    var itemsProp = AccessTools.Property(t, "MenuItems");
                    if (itemsProp == null) return;
                    if (!(itemsProp.GetValue(__instance) is System.Collections.IList items)) return;

                    var addItem = AccessTools.Method(t, "AddMenuItem", new[] { typeof(string), typeof(string), typeof(object) });
                    if (addItem == null) { Log("[ChaosMod] AddMenuItem not found"); return; }

                    int insertAt = Math.Max(0, items.Count - 1); // insert before the last item (Back)
                    var newItem = addItem.Invoke(__instance, new object[] { "Chaos", "Experience a random chaos effect every 30 seconds!", ChaosModeTag });

                    // AddMenuItem appends to the end — move it to just before Back.
                    if (items.Count > 0 && items[items.Count - 1] == newItem)
                    {
                        items.RemoveAt(items.Count - 1);
                        items.Insert(insertAt, newItem);
                    }

                    // Mark this instance as done so we never inject again.
                    _injectedInstances.Add(id);
                    Log("[ChaosMod] Injected 'Chaos' into GameModeMenu.");
                }
                catch (Exception ex)
                {
                    Log($"[ChaosMod] Failed to inject Chaos game mode: {ex.Message}");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Multiplayer sync: intercept CHAOSMOD| broadcast messages on all clients
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(CastleMinerZGame), "_processBroadcastTextMessage")]
        private static class Patch_ChaosSyncMessage
        {
            // Use __0 (positional) — Harmony injects by name, so if the original param isn't
            // called "message" the named form gets null. Positional always works.
            private static bool Prefix(object __0)
            {
                try
                {
                    if (!(__0 is BroadcastTextMessage btm)) return true;
                    if (string.IsNullOrEmpty(btm.Message) || !btm.Message.StartsWith("CHAOSMOD|")) return true;
                    string effectName = btm.Message.Substring("CHAOSMOD|".Length);
                    Log($"[ChaosMod] Received sync: {effectName}");

                    // ── Special control commands (not effect IDs) ──────────────
                    if (effectName == "CHAOS_START")  { ChaosEffects.ActivateChaosFromRemote(); return false; }
                    if (effectName == "CHAOS_STOP")   { ChaosEffects.DeactivateChaosFromRemote(); return false; }

                    // ── Regular effect sync ────────────────────────────────────
                    if (!Enum.TryParse(effectName, out ChaosEffectID effectId)) return true;
                    ChaosEffects.ExecuteSyncEffect(effectId);
                    return false; // suppress raw sync text from Console.WriteLine
                }
                catch (Exception ex) { Log($"[ChaosMod] ChaosSyncMessage error: {ex.Message}"); return true; }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Session join: show current gamemode + chaos status on HUD
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(CastleMinerZGame), "_processPlayerExistsMessage")]
        private static class Patch_ShowGamemodeOnJoin
        {
            // isEcho = true only for the local player's own PlayerExistsMessage echo —
            // i.e., the moment WE finish joining. That's when we want to show the gamemode.
            private static void Postfix(object __0, bool __1, bool __2)
            {
                try
                {
                    var game = CastleMinerZGame.Instance;
                    if (game?.CurrentNetworkSession == null) return;

                    if (!__1)
                    {
                        if (__2)
                            ChaosEffects.SyncChaosStateForJoinedPlayer();
                        return;
                    }

                    string modeName = "Unknown";
                    try { modeName = game.GameMode.ToString(); } catch { }
                    string diff = "Unknown";
                    try { diff = game.Difficulty.ToString(); } catch { }

                    string modeMsg = $"[Session] Gamemode: {modeName} — Difficulty: {diff}";
                    ChaosEffects.PostChat(modeMsg, Microsoft.Xna.Framework.Color.Cyan);
                    Log($"[ChaosMod] Joined session — {modeMsg}");

                    // If already in chaos, also show that.
                    if (ChaosSettings.IsChaosMode)
                        ChaosEffects.PostChat("[Chaos] CHAOS MODE is ACTIVE in this session!", Microsoft.Xna.Framework.Color.OrangeRed);
                }
                catch { }
            }
        }

        // Dynamic patches applied after startup for things we can't reference by type.
        public static void TryApplyDynamicPatches()
        {
            if (_harmony == null) return;
            TryPatchInventoryOpen();
            TryPatchTraySwitchBlock();
            TryPatchCraftBlock();
            TryPatchGameModeSelection();
        }

        private static void TryPatchGameModeSelection()
        {
            try
            {
                var asm = typeof(CastleMinerZGame).Assembly;
                var frontEndScreen = asm.GetType("DNA.CastleMinerZ.FrontEndScreen");
                if (frontEndScreen == null) { Log("[ChaosMod] FrontEndScreen not found"); return; }

                var m = AccessTools.Method(frontEndScreen, "_gameModeMenu_MenuItemSelected");
                if (m == null) { Log("[ChaosMod] _gameModeMenu_MenuItemSelected not found"); return; }

                var prefix = AccessTools.Method(typeof(GamePatches), nameof(HandleGameModeSelection));
                _harmony.Patch(m, prefix: new HarmonyMethod(prefix));
                Log("[ChaosMod] Patched _gameModeMenu_MenuItemSelected.");
            }
            catch (Exception ex)
            {
                Log($"[ChaosMod] Failed to patch game mode selection: {ex.Message}");
            }
        }

        // Helper: get the value of a member (field or property) by name from an object
        private static object GetMemberValue(object obj, string name)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) return f.GetValue(obj);
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null) return p.GetValue(obj, null);
            return null;
        }

        // Prefix: intercept game mode selection. Returns false (skip original) only when Chaos is chosen.
        private static bool HandleGameModeSelection(object __instance, object e)
        {
            bool isChaos = false;
            try
            {
                // Resolve e.MenuItem (may be field or property)
                var menuItem = GetMemberValue(e, "MenuItem");
                if (menuItem == null) { Log("[ChaosMod] HandleGameModeSelection: MenuItem is null"); return true; }

                // Resolve MenuItem.Tag (public field on MenuItemElement)
                var tag = GetMemberValue(menuItem, "Tag");
                Log($"[ChaosMod] GameMode selected tag={tag}");

                int tagValue;
                try { tagValue = Convert.ToInt32(tag); }
                catch
                {
                    ChaosSettings.IsChaosMode = false;
                    return true;
                }

                if (tagValue != 99)
                {
                    ChaosSettings.IsChaosMode = false;
                    return true; // not Chaos — let original run normally
                }

                // ── Chaos was clicked ───────────────────────────────────────
                isChaos = true;
                ChaosEffects.MarkChaosSelected(); // arms _chaosSelected + sets IsChaosMode
                Log("[ChaosMod] Chaos Mode selected — starting world.");

                var game = CastleMinerZGame.Instance;
                if (game == null) { Log("[ChaosMod] game is null"); return false; }

                // Set game settings for Chaos (Survival / Hardcore)
                game.GameMode = GameModeTypes.Survival;
                game.Difficulty = GameDifficultyTypes.HARDCORE;
                game.InfiniteResourceMode = false;
                game.JoinGamePolicy = DNA.Net.GamerServices.JoinGamePolicy.Anyone;

                // Call startWorld() on the FrontEndScreen (__instance)
                var startWorld = __instance?.GetType().GetMethod("startWorld",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (startWorld == null) { Log("[ChaosMod] startWorld not found"); return false; }
                startWorld.Invoke(__instance, null);
                Log("[ChaosMod] startWorld() called for Chaos Mode.");
            }
            catch (Exception ex)
            {
                Log($"[ChaosMod] HandleGameModeSelection error: {ex.Message}\n{ex.StackTrace}");
                if (!isChaos) return true; // if crash happened before we knew it was Chaos, don't block original
            }
            return false; // skip original — we handled it
        }

        private static void HandleGameModeSelectionPost(object __instance) { }  // no longer needed

        // (Chaos mode reset is handled in ChaosEffects.Tick when the game session ends.)

        private static void TryPatchPlayerJump()
        {
            try
            {
                var asm = typeof(CastleMinerZGame).Assembly;
                string[] typeCandidates = { "DNA.CastleMinerZ.PlayerEntity", "DNA.CastleMinerZ.LocalPlayerEntity",
                                            "DNA.CastleMinerZ.Player", "DNA.CastleMinerZ.LocalPlayer" };
                string[] jumpMethods   = { "Jump", "DoJump", "TryJump", "OnJump", "HandleJump" };
                foreach (string typeName in typeCandidates)
                {
                    var t = asm.GetType(typeName);
                    if (t == null) continue;
                    foreach (string mName in jumpMethods)
                    {
                        var m = AccessTools.Method(t, mName);
                        if (m == null) continue;
                        var prefix  = AccessTools.Method(typeof(GamePatches), nameof(BlockOrAirJumpPrefix));
                        _harmony.Patch(m, prefix: new HarmonyMethod(prefix));
                        Log($"[ChaosMod] Patched {typeName}.{mName} for NoJump/AirJump.");
                        return;
                    }
                }
                Log("[ChaosMod] No player Jump method found; NoJump/AirJump unavailable.");
            }
            catch (Exception ex) { Log($"[ChaosMod] TryPatchPlayerJump: {ex.Message}"); }
        }

        private static bool BlockOrAirJumpPrefix()
        {
            if (ChaosState.NoJump)  return false; // block jump entirely
            // AirJump: always allow (the flag just means the player can jump whenever — handled by skipping the on-ground check)
            return true;
        }

        private static void TryPatchInventoryOpen()
        {
            try
            {
                var asm = typeof(CastleMinerZGame).Assembly;
                string[] typeCandidates = { "DNA.CastleMinerZ.UI.InGameHUD", "DNA.CastleMinerZ.CastleMinerZGame" };
                string[] methodCandidates = { "OpenInventory", "ShowInventory", "ToggleInventory" };
                foreach (string typeName in typeCandidates)
                {
                    var t = asm.GetType(typeName);
                    if (t == null) continue;
                    foreach (string methodName in methodCandidates)
                    {
                        var m = AccessTools.Method(t, methodName);
                        if (m == null) continue;
                        var prefix = AccessTools.Method(typeof(GamePatches), nameof(BlockInventoryOpen));
                        _harmony.Patch(m, prefix: new HarmonyMethod(prefix));
                        return;
                    }
                }
            }
            catch { }
        }

        private static bool BlockInventoryOpen()
        {
            return !ChaosState.CantOpenInventory;
        }

        private static void TryPatchTraySwitchBlock()
        {
            try
            {
                var asm = typeof(CastleMinerZGame).Assembly;
                string[] methodCandidates = { "SwitchTray", "ToggleTray", "NextTray", "SwitchActiveTray" };
                foreach (var t in asm.GetTypes())
                {
                    foreach (string methodName in methodCandidates)
                    {
                        var m = AccessTools.Method(t, methodName);
                        if (m == null) continue;
                        var prefix = AccessTools.Method(typeof(GamePatches), nameof(BlockTraySwitch));
                        _harmony.Patch(m, prefix: new HarmonyMethod(prefix));
                        return;
                    }
                }
            }
            catch { }
        }

        private static bool BlockTraySwitch()
        {
            return !ChaosState.CantSwitchTrays;
        }

        private static void TryPatchCraftBlock()
        {
            try
            {
                var asm = typeof(CastleMinerZGame).Assembly;
                string[] methodCandidates = { "Craft", "CraftItem", "TryCraft" };
                foreach (var t in asm.GetTypes())
                {
                    foreach (string methodName in methodCandidates)
                    {
                        var m = AccessTools.Method(t, methodName);
                        if (m == null) continue;
                        var prefix = AccessTools.Method(typeof(GamePatches), nameof(BlockCraft));
                        _harmony.Patch(m, prefix: new HarmonyMethod(prefix));
                        return;
                    }
                }
            }
            catch { }
        }

        private static bool BlockCraft()
        {
            return !ChaosState.CantCraft;
        }
    }
}
