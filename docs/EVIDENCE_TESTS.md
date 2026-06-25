# Evidence Tests

The evidence tests are repeatable RimBridge-driven scenarios that produce files suitable for visual and behavioral review.

## Tank And Pipe: 16 Aim Close-Ups

Run:

```bash
./scripts/run-tank-pipe-evidence.sh
```

What it does:

- Copies `Originals/zeflammenwerfer walkthrough.rws` into the configured RimWorld save folder.
- Starts or connects to GABS game id `rimworld-direct`.
- Runs `zeflammenwerfer/render_tank_pipe_pose_sweep` through the RimBridgeServer v2 companion SDK.
- Loads the `zeflammenwerfer walkthrough` save to visual readiness.
- Poses Rocha with 16 aim targets around his occupied cell using the mod-owned `zeflammenwerfer/set_tank_pipe_pose` tool.
- Captures each pose with `rimworld/screenshot_cell_rect`.
- Copies the generated PNG files and a `manifest.json` into `artifacts/rimbridge-evidence/tank-pipe-16-aims-sdk/<run-id>/`.

The screenshot request is intentionally narrow:

```text
x = 117
z = 114
width = 1
height = 1
paddingCells = 2
rootSize = 2.5
```

That means the evidence target is Rocha's single occupied cell. RimBridge's cell-rect screenshot tool provides the crop and safe framing around that cell.

The pose sweep is intentionally in C#, not hard-coded as Lua target-cell math. Use `zeflammenwerfer/list_tank_pipe_pose_sweep` to inspect the current 16-pose definition, `zeflammenwerfer/set_tank_pipe_pose` to apply one pose by one-based index, or `zeflammenwerfer/render_tank_pipe_pose_sweep` to run the full async screenshot harness.

Useful overrides:

```bash
./scripts/run-tank-pipe-evidence.sh --run-id manual-check
./scripts/run-tank-pipe-evidence.sh --padding-cells 1 --root-size 2.0
./scripts/run-tank-pipe-evidence.sh --force-takeover
```

## Flame Tests

Flame behavior tests should use the same companion-tool pattern, but they should not freeze the particle system at the setup point. Set up the target state, estimate the number of ticks needed for the flame particles to travel and collide, then advance real RimWorld ticks through the SDK:

```csharp
var tick = await ctx.Game.StepTicksAsync(estimatedTicks, new RimBridgeTickOptions
{
	TimeoutMs = timeoutMs,
	PauseFirst = true
}, cancellationToken: cancellationToken);
```

`ctx.Game.StepTicksAsync` advances the paused game through real Unity update frames, so particle systems and collision callbacks run through realistic behavior instead of being inspected as static state.
