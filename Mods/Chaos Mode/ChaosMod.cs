using System;
using System.IO;
using System.Reflection;
using DNA.CastleMinerZ;
using DNA.Input;
using Microsoft.Xna.Framework;
using ModLoader;

using static ModLoader.LogSystem;

namespace ChaosMod
{
    [Priority(Priority.Normal)]
    [RequiredDependencies("")]
    public class ChaosMod : ModBase
    {
        private static DateTime _lastConfigWriteUtc = DateTime.MinValue;
        private static int _lastConfigPollTick;

        public ChaosMod() : base("ChaosMod", new Version("1.0.0"))
        {
            var game = CastleMinerZGame.Instance;
            if (game != null)
                game.Exiting += (s, e) => Shutdown();
        }

        public override void Start()
        {
            if (CastleMinerZGame.Instance == null)
            {
                Log("[ChaosMod] Game instance is null.");
                return;
            }

            ChaosConfig.LoadApply();
            GamePatches.ApplyAllPatches();
            GamePatches.TryApplyDynamicPatches();
            ChaosEffects.Init();

            Log($"[ChaosMod] Loaded. Interval={ChaosSettings.ChaosInterval}s, EffectDuration={ChaosSettings.EffectDuration}s");
        }

        public static void Shutdown()
        {
            try
            {
                GamePatches.DisableAll();
                Log("[ChaosMod] Shutdown complete.");
            }
            catch (Exception ex)
            {
                Log($"[ChaosMod] Shutdown error: {ex.Message}");
            }
        }

        public override void Tick(InputManager inputManager, GameTime gameTime)
        {
            // Hot-reload config every second.
            int now = Environment.TickCount;
            uint dt = unchecked((uint)(now - _lastConfigPollTick));
            if (dt >= 1000u)
            {
                _lastConfigPollTick = now;
                try
                {
                    string path = ChaosConfig.ConfigPath;
                    if (File.Exists(path))
                    {
                        DateTime writeUtc = File.GetLastWriteTimeUtc(path);
                        if (writeUtc > _lastConfigWriteUtc)
                        {
                            _lastConfigWriteUtc = writeUtc;
                            ChaosConfig.LoadApply();
                        }
                    }
                }
                catch { }
            }

            if (!ChaosSettings.Enabled) return;

            // Run chaos logic with real elapsed time.
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (elapsed <= 0f || elapsed > 1f) elapsed = 1f / 60f;

            ChaosEffects.Tick(elapsed);
        }
    }
}
