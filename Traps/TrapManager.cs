using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RL2Archipelago.Traps;

/// <summary>
/// Manages NG+ burden trap effects received from the Archipelago multiworld.
/// When a trap item is granted, the corresponding biome hazard (cannonball rain,
/// dragon lancers, etc.) is activated for the current run and fires in every
/// valid room until the player dies.
/// </summary>
internal static class TrapManager
{
    internal static bool CannonballRainActive;
    internal static bool DragonLancersActive;
    internal static bool AutomatonSwarmActive;
    internal static bool GiantSnowflakesActive;
    internal static bool VoidWavesActive;

    private static bool _subscribed;

    // Each entry tracks a running attack coroutine so we can stop it on room exit or death.
    private static readonly List<(object rule, BaseRoom room, Coroutine coroutine)> _activeCoroutines = new();

    // Stable delegate instances. required so RemoveListener can match the exact reference.
    private static readonly Action<object, EventArgs> _onEnterRoom = OnPlayerEnterRoom;
    private static readonly Action<object, EventArgs> _onExitRoom  = OnPlayerExitRoom;

    // ── Public API ──────────────────────────────────────────────────────────

    internal static void ActivateTrap(BurdenType burdenType)
    {
        switch (burdenType)
        {
            case BurdenType.BridgeBiomeUp:
                CannonballRainActive = true;
                EnsurePooled("BridgeBiomeUpCannonBallWarningProjectile",
                             "BridgeBiomeUpCannonBallProjectile");
                break;

            case BurdenType.TowerBiomeUp:
                DragonLancersActive = true;
                EnsurePooled("TowerBiomeUpWarningProjectile",
                             "TowerBiomeUpExplosionProjectile",
                             "TowerBiomeUpLancerProjectile");
                break;

            case BurdenType.ForestBiomeUp:
                GiantSnowflakesActive = true;
                EnsurePooled("ForestBiomeUpSwirlWarningProjectile",
                             "ForestBiomeUpExplosionProjectile");
                break;

            case BurdenType.CaveBiomeUp:
                AutomatonSwarmActive = true;
                EnsurePooled("CaveBossWaveWarningProjectile",
                             "CaveBossWaveWarningLockedProjectile",
                             "CaveBossWaveProjectile");
                break;

            case BurdenType.StudyBiomeUp:
                VoidWavesActive = true;
                EnsurePooled("StudyBiomeUpVoidWarningProjectile",
                             "StudyBiomeUpVoidProjectile");
                break;
        }

        if (!_subscribed)
        {
            Messenger<GameMessenger, GameEvent>.AddListener(GameEvent.PlayerEnterRoom, _onEnterRoom);
            Messenger<GameMessenger, GameEvent>.AddListener(GameEvent.PlayerExitRoom,  _onExitRoom);
            _subscribed = true;
        }
    }

    internal static void ClearAllTraps()
    {
        CannonballRainActive  = false;
        DragonLancersActive   = false;
        AutomatonSwarmActive  = false;
        GiantSnowflakesActive = false;
        VoidWavesActive       = false;

        StopAll();

        if (_subscribed)
        {
            Messenger<GameMessenger, GameEvent>.RemoveListener(GameEvent.PlayerEnterRoom, _onEnterRoom);
            Messenger<GameMessenger, GameEvent>.RemoveListener(GameEvent.PlayerExitRoom,  _onExitRoom);
            _subscribed = false;
        }
    }

    // ── Room event handlers ─────────────────────────────────────────────────

    private static void OnPlayerEnterRoom(object sender, EventArgs args)
    {
        if (!(args is RoomViaDoorEventArgs roomArgs)) return;
        var room = roomArgs.Room;
        if (room == null || !IsValidTrapRoom(room)) return;

        // Biome transitions call CreatePoolsFromQueuedProjectiles which destroys any pool
        // entry not in its queue, including ones we added. Re-pool on every room entry so
        // the projectiles are always present when the coroutine tries to fire them.
        EnsureActiveTrapsPooled();

        if (CannonballRainActive)
            TryStartTrap<BridgeBiomeUp_BiomeRule>(room, "m_attackCoroutine",      "Attack_Coroutine");
        if (DragonLancersActive)
            TryStartTrap<TowerBiomeUp_BiomeRule> (room, "m_lancerAttackCoroutine","Lancer_Attack_Coroutine");
        if (GiantSnowflakesActive)
            TryStartTrap<ForestBiomeUp_BiomeRule>(room, "m_forestAttackCoroutine","ForestAttackCoroutine");
        if (AutomatonSwarmActive)
            TryStartTrap<CaveBiomeUp_BiomeRule>  (room, "m_waveAttackCoroutine",  "Wave_Attack_Coroutine");
        if (VoidWavesActive)
            TryStartTrap<StudyBiomeUp_BiomeRule> (room, "m_voidAttackCoroutine",  "Void_Attack_Coroutine");
    }

    private static void EnsureActiveTrapsPooled()
    {
        if (CannonballRainActive)
            EnsurePooled("BridgeBiomeUpCannonBallWarningProjectile",
                         "BridgeBiomeUpCannonBallProjectile");
        if (DragonLancersActive)
            EnsurePooled("TowerBiomeUpWarningProjectile",
                         "TowerBiomeUpExplosionProjectile",
                         "TowerBiomeUpLancerProjectile");
        if (GiantSnowflakesActive)
            EnsurePooled("ForestBiomeUpSwirlWarningProjectile",
                         "ForestBiomeUpExplosionProjectile");
        if (AutomatonSwarmActive)
            EnsurePooled("CaveBossWaveWarningProjectile",
                         "CaveBossWaveWarningLockedProjectile",
                         "CaveBossWaveProjectile");
        if (VoidWavesActive)
            EnsurePooled("StudyBiomeUpVoidWarningProjectile",
                         "StudyBiomeUpVoidProjectile");
    }

    private static void OnPlayerExitRoom(object sender, EventArgs args)
    {
        StopAll();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static void TryStartTrap<T>(BaseRoom room, string coroutineField, string coroutineMethod)
        where T : class
    {
        var rule = FindBiomeRule<T>();
        if (rule == null)
        {
            Plugin.Log.LogWarning($"[AP] TrapManager: could not find {typeof(T).Name}");
            return;
        }

        // TowerBiomeUp_BiomeRule allocates its projectile/coroutine arrays in Initialize().
        // Call it here rather than in ActivateTrap so it always runs against a live rule instance,
        // even when the trap is restored from a saved run before BiomeRuleManager is in the scene.
        if (rule is TowerBiomeUp_BiomeRule)
        {
            var init = Traverse.Create(rule);
            if (!init.Field("m_isInitialized").GetValue<bool>())
                init.Method("Initialize").GetValue();
        }

        var traverse = Traverse.Create(rule);
        traverse.Field("m_currentRoom").SetValue(room);

        var ienumerator = (IEnumerator)traverse.Method(coroutineMethod).GetValue();
        var coroutine   = room.StartCoroutine(ienumerator);

        // Mirror into the rule's own field so its cleanup code stays consistent.
        traverse.Field(coroutineField).SetValue(coroutine);

        _activeCoroutines.Add((rule, room, coroutine));
    }

    private static void StopAll()
    {
        foreach (var (rule, room, coroutine) in _activeCoroutines)
        {
            try
            {
                if (coroutine != null && room != null)
                    room.StopCoroutine(coroutine);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[AP] TrapManager: error stopping coroutine: {ex.Message}");
            }

            Traverse.Create(rule).Field("m_currentRoom").SetValue(null);
        }
        _activeCoroutines.Clear();
    }

    /// <summary>
    /// Returns true for Standard and Trap rooms that are not tunnel destinations
    /// or boss entrances.
    /// </summary>
    private static bool IsValidTrapRoom(BaseRoom room)
    {
        if (room.RoomType != RoomType.Standard && room.RoomType != RoomType.Trap)
            return false;

        var room2     = room as Room;
        var mergeRoom = room as MergeRoom;

        // Boss-entrance rooms are large merged rooms; avoid spawning hazards there.
        if (mergeRoom != null)
        {
            foreach (var gpm in mergeRoom.StandaloneGridPointManagers)
            {
                if (gpm.RoomMetaData.SpecialRoomType == SpecialRoomType.BossEntrance)
                    return false;
            }
        }

        // Tunnel destination rooms are transition corridors. skip them.
        if (room2     != null) return !room2.GridPointManager.IsTunnelDestination;
        if (mergeRoom != null) return !mergeRoom.StandaloneGridPointManagers[0].IsTunnelDestination;

        return true;
    }

    /// <summary>
    /// Walks the <see cref="BiomeRuleManager"/>'s serialized rule list to find
    /// the singleton ScriptableObject instance of type <typeparamref name="T"/>.
    /// </summary>
    private static T FindBiomeRule<T>() where T : class
    {
        var manager = UnityEngine.Object.FindObjectOfType<BiomeRuleManager>();
        if (manager == null) return null;

        var entries = Traverse.Create(manager)
                              .Field("m_biomeRules")
                              .GetValue<List<BiomeRuleManagerEntry>>();
        if (entries == null) return null;

        foreach (var entry in entries)
        {
            if (entry?.Rules == null) continue;
            foreach (var rule in entry.Rules)
            {
                if (rule is T typedRule) return typedRule;
            }
        }
        return null;
    }

    private static void EnsurePooled(params string[] names)
    {
        if (ProjectileManager.Instance == null) return;
        foreach (var name in names)
            ProjectileManager.Instance.AddProjectileToPool(name);
    }
}
