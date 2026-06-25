# Technical Review

Review date: 2026-06-25
Baseline commit: `774d86f Establish modern mod infrastructure baseline`
Target game build: RimWorld `1.6.4850`

This review covers the runtime behavior after the infrastructure lift. It is based on source inspection, a Release build, package audits, and targeted decompiler checks against the locally installed RimWorld `Assembly-CSharp.dll`.

## Summary

The mod is buildable and deployable with the new infrastructure, and the main Harmony target names used by the mod still exist in RimWorld 1.6.4850. The risky areas are not missing symbols; they are incomplete lifecycle cleanup, broad transpiler assumptions, runtime-only state that does not survive save/load, and a few places where null or shield edge cases can leave objects behind.

The preferred live validation save is `zeflammenwerfer walkthrough`. It has a prepared test setup. `Dev quicktest` remains useful for fresh-colony smoke checks.

Live bridge validation uses the offline non-Steam RimWorld copy through GABS `rimworld-direct`, then loads `zeflammenwerfer walkthrough`. Use generic RimBridge tools for loading, ticking, screenshots, colonists, and logs; use this mod's `zeflammenwerfer/*` tools for flamethrower internals such as render state, fuel state, flame blockers, and damage probes.

## High Priority Findings

### Unequip subscriber postfix has the wrong instance type

File: `Source/PawnExtension.cs`

`PawnExtension.Notify_Unequipped` patches `ThingWithComps.Notify_Unequipped(Pawn pawn)` but declares the Harmony instance parameter as `Pawn __instance`.

```csharp
public static void Notify_Unequipped(Pawn __instance, Pawn pawn)
{
	if (pawn == null || __instance.def != Defs.ZeFlammenwerfer)
		return;
```

In RimWorld 1.6.4850, `Pawn_EquipmentTracker.Notify_EquipmentRemoved(eq)` calls `eq.Notify_Unequipped(pawn)`, and the target method instance is the equipment. With the current signature, the def check is effectively against the pawn, so the subscriber `Unequipped` callbacks do not fire for the flamethrower.

Impact:

- `PawnShooterTracker.Unequipped` is skipped.
- Shooter `FlameRadiusDetector` Unity objects can remain registered after the weapon is unequipped until another path happens to unregister or update them.
- `PawnTargetTracker` can continue registering target colliders based on a stale shooter tracker.

The flamethrower override and equipment removal prefix still clear some weapon state, so this is not a total cleanup failure. The subscriber path is still broken and should be fixed.

Recommended fix:

- Change the postfix signature to `ThingWithComps __instance`.
- Add an equip/unequip validation scenario that checks `PawnShooterTracker.trackers` and `ColliderHolder.holders` return to the expected counts.

### Target range check uses PLINQ over live RimWorld and Unity objects

File: `Source/PawnShooterTracker.cs`

`PawnShooterTracker.InRange` uses `trackers.Keys.AsParallel()` and reads `shooter.Map` plus `shooter.drawer.tweener.tweenedPos` from worker threads.

```csharp
return trackers.Keys
	.AsParallel()
	.Any(shooter => shooter.Map == map &&
		(shooter.drawer.tweener.tweenedPos - position).MagnitudeHorizontalSquared() <= FlameRadiusDetector.maxRadiusSquared);
```

Impact:

- Unity and RimWorld object access should stay on the main thread.
- The dictionary can be modified by equip, despawn, load, or cleanup paths while PLINQ is enumerating it.
- A small number of shooters does not justify the concurrency risk.

Recommended fix:

- Replace this with a main-thread `foreach` over `trackers.Keys.ToArray()` or a locked/snapshotted list.
- Guard the input pawn, map, drawer, and tweener before reading draw positions.

### Shield-blocked flame projectiles skip cleanup

File: `Source/ZeFlame.cs`

`ZeFlame.Impact` immediately returns when `blockedByShield` is true.

```csharp
if (blockedByShield) // shield does not block
	return;
```

The cleanup path that destroys the projectile and removes it from `flameComp.flames` is below that return. RimWorld's base `Projectile.Impact` destroys the projectile, and vanilla RimWorld's `FlameThrower.Impact` calls base before doing its custom fire work. This mod bypasses base and therefore needs to perform its own cleanup even when a shield is involved.

Impact:

- Shield-belt interactions can leave a `ZeFlame` in `flameComp.flames`.
- The visual/audio active state can last longer than intended because `ZeFlameComp.ShouldStayActiveBetweenShots` and active flame tracking see stale projectiles.

Recommended fix:

- Treat shield blocking as non-blocking for this projectile by continuing to the normal cleanup path.
- Or explicitly destroy and remove the projectile before returning.
- Add a shield-belt target test.

### Refuel job fail conditions can dereference a removed weapon

File: `Source/FlamethrowerRefuelJobDriver.cs`

`MakeNewToils` has null-safe end conditions, but the next fail conditions dereference `RefuelableComp` directly.

```csharp
AddEndCondition(() => RefuelableComp == null ? JobCondition.Incompletable : ...);
AddFailCondition(() => !job.playerForced && !RefuelableComp.ShouldAutoRefuelNowIgnoringFuelPct);
AddFailCondition(() => !RefuelableComp.allowAutoRefuel && !job.playerForced);
```

Impact:

- If the bearer unequips, drops, destroys, or swaps the weapon while another pawn is en route to refuel it, the job can throw a null reference instead of failing cleanly.
- This is likely during combat, drafting, downing, or user-forced equipment changes.

Recommended fix:

- Cache `var comp = RefuelableComp` inside each condition and fail if it is null.
- Consider failing if `Flamethrower` is no longer the bearer's current primary weapon.

### Wall damage from direct flame blasts appears too low

Files: `Source/CollisionHandler.cs`, `Source/Tools.cs`, `Source/Patches.cs`, `Defs/Weapon.xml`

Observed live behavior: a full blast into a wall only removed a few hit points, which is lower than expected and reportedly different from earlier behavior.

The projectile def itself has no direct projectile damage, so practical wall damage depends on Unity particle collisions calling `ThingCollisionHandler.OnParticleCollision`, which computes an `amount` from particle velocity and shooter skill, then calls `Tools.ApplyFlameDamage`. The later actual HP loss is mediated through attached fire and the `Fire.DoFireDamage` transpiler multiplier path.

Likely investigation targets:

- Whether wall particle collisions are firing often enough and resolving the intended wall cell.
- Whether `amount = max(abs(v.x), abs(v.z)) / (21 - skill)` has become too small for current particle velocities or test shooters.
- Whether the `Fire.DoFireDamage` multiplier path now reduces building damage too aggressively via `factorThing = 0.1f`.
- Whether attached/cell fire size is too low to produce the previous expected sustained wall damage.

Recommended validation:

- Use `zeflammenwerfer/get_damage_state` before and after real wall firing.
- Use `zeflammenwerfer/apply_flame_damage_probe` on the same wall and compare that controlled path against real projectile/particle impact.
- Use `zeflammenwerfer/probe_fire_line` and `zeflammenwerfer/get_flame_collision_state` to confirm the wall is the first particle blocker and has a registered collider.

## Medium Priority Findings

### Runtime-added FireDamage comp is not durable across save/load

Files: `Source/Tools.cs`, `Source/FireDamage.cs`

`Tools.ApplyFlameDamage` dynamically creates a `FireDamage` comp and appends it to `thing.comps`. Decompiler checks against RimWorld 1.6.4850 show that `ThingWithComps.Tick` will tick the live list, so the comp works while the thing remains loaded.

The load boundary is different: `ThingWithComps.ExposeData` calls `InitializeComps()` during `LoadingVars`, and `InitializeComps()` rebuilds `comps` from `def.comps`. A dynamically added comp that is not present on the ThingDef is not recreated on load.

Impact:

- The `FireDamage.multiplier` state is transient.
- Saving and loading while a target is burning can reset the multiplier and mech-specific flammability/damage behavior until the thing is hit again.
- The save may contain comp data that no longer maps to a loaded comp list.

Recommended fix:

- Move this state to a `GameComponent` or `MapComponent` keyed by thing reference/load ID.
- Alternatively model it as a hediff for pawns and a dedicated thing tracker for non-pawns.

### Persistent pathing cost grids need stronger map/job lifetime handling

File: `Source/FlameDangerTracker.cs`

`FlameDangerTracker` stores one persistent `NativeArray<ushort>` per active map state and passes read-only views into `PathGridJob.custom`.

```csharp
routeCosts = new NativeArray<ushort>(map.cellIndices.NumGridCells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
...
job.custom = routeCosts;
```

The code disposes a map state when the active cell count reaches zero and clears all state on new game/load. There is no explicit map-removal hook, and there is no visible coordination with outstanding path jobs that may already hold a read-only view.

Impact:

- Unusual map removal, temporary maps, or load-order failures can retain map references and persistent native allocations until the next full game load.
- If a path job can outlive the moment danger state is cleared, `job.custom` can point at disposed native memory.

Recommended fix:

- Add explicit cleanup for map removal/finalization lifecycle.
- Audit whether `PathGridJob.custom` is consumed synchronously or asynchronously in 1.6.4850.
- Prefer keeping an empty array alive until no outstanding path jobs can reference it, or use a central long-lived per-map grid that is zeroed instead of disposed immediately.

### Flame radius detector cleanup depends on subscriber paths

Files: `Source/PawnShooterTracker.cs`, `Source/FlameRadiusDetector.cs`, `Source/PawnExtension.cs`

`FlameRadiusDetector.Update` removes cell colliders when the shooter no longer has the flamethrower, but that cleanup only happens if an update is invoked. With the unequip subscriber bug, a pawn that unequips and then stands still can keep its detector until another lifecycle path removes it.

Impact:

- Hidden Unity colliders can stay active after the weapon state changed.
- Collision behavior can be wrong for nearby pawns or blockers until the next movement/load/despawn event.

Recommended fix:

- Fix the unequip subscriber first.
- Make `Pawn_EquipmentTracker_Notify_EquipmentRemoved_Patch` also unregister shooter trackers directly for belt-and-braces cleanup.

### Harmony transpiler and private-method hooks are current but fragile

File: `Source/Patches.cs`

Decompiler checks confirmed the relevant RimWorld 1.6.4850 symbols exist:

- `Pawn_EquipmentTracker.<GetGizmos>g__YieldGizmos|30_0(ThingWithComps eq, KeyBindingDef preferredHotKey)`
- `Pawn_StanceTracker.StanceTrackerTick()`
- `Fire.DoFireDamage(Thing targ)`
- `PathFinderMapData.ParameterizeGridJob(...)`
- `Pawn_PathFollower.SetupMoveIntoNextCell()`
- `ThingWithComps.Notify_Unequipped(Pawn pawn)`
- `RefuelWorkGiverUtility.FindBestFuel(...)`
- `RefuelWorkGiverUtility.FindAllFuel(...)`

The symbols exist, but several hooks are brittle:

- `Pawn_EquipmentTracker_YieldGizmos_Patch.TargetMethod` selects the first method whose name contains `__YieldGizmos` and has a first `ThingWithComps` parameter.
- `Pawn_StanceTracker_StanceTrackerTick_Patch.Transpiler` injects before every branch. RimWorld 1.6.4850 currently has one branch in the method, so it works now; a future extra branch would change behavior.
- `Fire_DoFireDamage_Patch.Transpiler` is better anchored, but still assumes the first `ldarg.1` plus `isinst Pawn` pair remains where the damage local is ready.

Recommended fix:

- Make target-method selection fail loudly with a clear error if the expected method is not found.
- Narrow the stance transpiler to the specific `get_FullBodyBusy` branch, not every branch.
- Add a small reflection/decompiler smoke-test script or unit-style check that verifies target methods before release.

## Lower Priority Findings

### `Thing.Position` flame trail patch has weak null assumptions

File: `Source/Patches.cs`

The flame trail patch reads `flame.Launcher.Position` without a null/destroyed guard.

```csharp
if (map != null && value.DistanceToSquared(flame.Launcher.Position) > 4)
```

This is probably safe during normal launched projectile movement, but it is fragile around spawn-before-launch, destroyed launchers, or malformed projectiles from dev tools.

Recommended fix:

- Guard `flame.Launcher?.Spawned == true` before reading its position.

### Fire collision enumeration is lazy while effects can mutate the cell

File: `Source/CollisionHandler.cs`

`ThingCollisionHandler.OnParticleCollision` creates a lazy `IEnumerable<Thing>` from `thingGrid.ThingsAt(cell).Where(...)`, then applies flame damage and starts cell fire using the same enumerable.

```csharp
var things = thingGrid.ThingsAt(cell).Where(thing => thing as Pawn == null);
things.OfType<ThingWithComps>().Do(...);
Tools.ApplyCellFlame(map, amount, cell, things);
```

If applying damage or spawning fire mutates the thing list, this can become order-sensitive.

Recommended fix:

- Materialize to `ToArray()` before applying effects.

### Audio global set is only useful if every comp removes cleanly

File: `Source/ZeFlameSound.cs`

`ZeFlameSound.allFlameSounds` tracks sounds, but there is no bulk cleanup path over that set. Normal `ZeFlameComp.Remove` calls `sound.Remove`, so this is only a problem when a comp cleanup is missed.

Recommended fix:

- If lifecycle cleanup is hardened, either remove the unused global set or add a bulk cleanup method called from game-load/main-menu cleanup.

## Dependency Status

NuGet direct package references were checked with:

```sh
dotnet list Source/ZeFlammenwerfer.csproj package --outdated
dotnet list Source/ZeFlammenwerfer.csproj package --deprecated
dotnet list Source/ZeFlammenwerfer.csproj package --vulnerable --include-transitive
```

Result:

- Direct NuGet package references are current for the configured package sources.
- No deprecated packages were reported.
- No vulnerable packages were reported.
- Transitive framework/build packages are reported as outdated, mostly `Microsoft.Build.*` and old `System.*` support libraries under `net472`. These are not direct mod runtime dependencies and should not be forced into the project unless a concrete build problem appears.

Unity asset project notes:

- The project uses Unity `2022.3.62f3` locally.
- `com.unity.ai.navigation` is pinned to `1.1.6`.
- `com.unity.ide.visualstudio` is pinned to `2.0.22`.
- The registry currently has newer package versions, but `com.unity.ai.navigation@2.0.13` declares Unity `6000.0`, so latest-registry is not automatically compatible with this asset project.
- Because this Unity project is only used to produce asset bundles, Unity package upgrades are low priority unless asset builds break or the editor cannot resolve the project.

## Recommended Test Matrix

Use the installed mod copy under the RimWorld `Mods` directory. Prefer the save named `zeflammenwerfer walkthrough` for scenario testing; use `Dev quicktest` for clean fresh-colony smoke checks.

- New colony load with this mod, Harmony, and RimBridgeServer enabled.
- Equip, fire at a cell, fire at a pawn, fire at a mech, and fire at a flammable building.
- Fire a sustained blast into a wall, record HP before/after with `zeflammenwerfer/get_damage_state`, and compare to `zeflammenwerfer/apply_flame_damage_probe`.
- Equip and unequip without moving, then verify no stale shooter or target colliders remain.
- Shoot a shield-belt pawn and verify projectiles, flame visuals, and sounds stop cleanly.
- Start an equipped-refuel job, then remove/swap/drop the bearer's flamethrower while the refuel pawn is en route.
- Save/load while the flame visual is active and while targets are burning.
- Switch between multiple maps while a flamethrower is firing.
- Remove or abandon a temporary map after flame danger cells were active.
- Watch the Player.log through `scripts/summarize-rimworld-log.sh`.

## Suggested Fix Order

1. Fix the unequip subscriber signature and remove PLINQ from `PawnShooterTracker.InRange`.
2. Harden refuel job fail conditions and final toil null handling.
3. Fix shield-blocked projectile cleanup.
4. Decide how `FireDamage` should persist across save/load.
5. Harden `FlameDangerTracker` map/native-array lifetime.
6. Narrow the broad transpiler anchors and add release-time patch target checks.
7. Run the live RimWorld/GABS test matrix.
