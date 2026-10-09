# Workspace instructions

The user explicitly prohibited player builds on 2026-10-04. Do not run Android, iOS, desktop or other player builds, APK/AAB packaging, or scripts that launch them unless the user explicitly asks for a build again. Implement changes and use isolated Editor/EditMode/PlayMode checks instead. Do not install or publish a new build as part of routine verification.

# Host resource rules (8GB RAM / 4 cores, parallel agents)

User override on 2026-10-05: the user explicitly authorized removing Unity resource
limits to render the seasonal scenes. `unity-limit.slice` now has unlimited CPU,
MemoryHigh, MemoryMax and MemorySwapMax through persistent systemd property drop-ins.
Its nested scopes still fail with `Structure needs cleaning`; launch the single
authorized Unity Editor check directly if needed. sys-guard may move it into the
now-unlimited slice. Keep one Unity run at a time and the worker counts below.
This override does not authorize player builds or interference with other sessions.

- Check `~/.local/state/sys-guard/status` before heavy work; if `MEM_AVAIL_MB < 1500` or `STATE=hard|crit`, postpone or run `~/.local/bin/sys-clean.sh` first. Keep `df -h /` >10% free.
- Run Unity batch/tests inside the capped slice: `systemd-run --user --scope --slice=unity-limit.slice nice -n10 ionice -c2 -n7 <cmd>` with `-job-worker-count 1 -background-job-worker-count 4`. sys-guard enforces the cap automatically — do not move processes out or renice them. One Unity run at a time.
- Never kill processes you did not start; especially `adb`, `scrcpy`, `devin`, `codex`, `code`, `node`, `python`, shells, desktop/session services. A phone-testing agent owns `adb` and `/tmp/qcb_*` — do not touch.
- Clean up your artifacts: `TestResults/` files older than 21 days and stale `*.log`/`*.xml` are auto-quarantined (restorable via `sys-clean.sh --restore`). Keep the repo lean.
- Clones under `~/.cache/quietcamp` are disposable; idle clones get `Library/Temp/Logs` pruned automatically. `device-backup*` and `package-backups` are never touched.
- Do not modify `sys-guard.service`, `sys-clean.timer`, `unity-limit.slice`, `auto-nice.service`, or earlyoom.

# Unity and active phone sessions

On 2026-10-06, an isolated Editor render launch automatically terminated an
external-SDK ADB server during Android device scanning. This can happen even
without building or issuing ADB commands. Unity's "Kill external ADB instances"
and "Kill ADB server on exit" defaults must be accounted for before any Editor,
render or build run alongside phone work. The user declined changing these
shared Editor settings, so fresh render/build launches remain blocked until a
safe, explicitly approved arrangement is available. Never restart ADB or modify
another agent's phone session to recover from this issue.

The existing seasonal gallery is dated 2026-10-05; the attempted 2026-10-06
showcase renders failed and must not be advertised as passed or freshly captured.

# Monetization direction and lightweight verification

The user's 2026-10-05 direction supersedes the initial paid-intro proposal: retain
the existing main story, add separately unlockable story journeys, start with three
hints, use lives for incorrect checks (clear all tents/history), cap rewarded-ad
lives at ten per UTC day, and provide Pro without resource limits/counters. Five
initial lives and currency exchange prices are prototype values in
`QuietCamp/Assets/QuietCamp/Resources/QuietCamp/monetization.json`. No real store,
ad inventory, backend or billing products were provided; purchases stay disabled.
The lighthouse's eight frozen levels are staging content, not an approved product.

Run from the repository root, one verification job at a time:
- `dotnet build tools/MonetizationProbe.csproj -m:1 -p:UseSharedCompilation=false && dotnet run --no-build --project tools/MonetizationProbe.csproj`
  executes isolated contracts using the real domain/session/save pipeline, platform
  doubles and synthetic temporary slots. It also validates all 30 main and eight
  lighthouse puzzles, witnesses, independent solver results and new uk/en/de keys.
- `python3 tools/verify_monetization.py` compiles current runtime and Editor/PlayMode
  test sources against the existing Unity Bee response files and cached references.
  It does not launch Unity, import assets, run scene tests or build a player.

These checks do not prove rendered UI, mobile performance, real purchases/ad
rewards, human interest or production security of wallet/entitlement data. The
local ad-day clock and editable prototype caches need an authoritative backend
before real-money activation. Do not enable sales simply because compilation or
fake-provider tests pass.

# Continuous delivery rule (user direction, 2026-10-07)

The user asked agents to commit and push finished work continuously instead of
holding large uncommitted batches. Commit and `git push` after every completed
verification step or coherent milestone; do not wait for a final state. Keep
project documentation and the repo description current: when visuals or systems
change, update the relevant docs (`CAMPAIGN_ROADMAP.md`, README if applicable)
and refresh showcase screenshots when a rendered capture is actually verified.
Never claim renders, tests or builds as fresh when they are stale, skipped or
failed.

# Roadmap scene composition authoring

Before editing roadmap geography, ensembles or asset placement, read
[the Ukrainian tool guide](tools/scene-composition/USAGE_UA.md),
[CLI quick start](tools/scene-composition/README.md) and
[diagnostic reference](tools/scene-composition/DIAGNOSTICS.md).

- Edit compact sources in `QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition`.
  Do not hand-edit generated transforms, meshes or the runtime catalog.
- Use `inspect → patch --expected-hash → validate → compose --dry-run`.
  Patch the source owner/template returned by inspect, rather than a generated child.
- Keep yards, fences, gates and approaches as owned ensembles. Power routes use
  typed supports/conductor sockets; preserve node IDs, order and unlock rules.
- Portable `bake` prepares placements only. Native meshes/publication require
  `Quiet Camp/Composition/Bake Main` in Unity under the existing host/ADB rules.
  CLI `preview` does not launch Unity. Do not infer permission to launch it from this section.
- Validate actual native renders before claiming visual acceptance. Cached compiler
  checks and portable planner timings do not prove mobile FPS or native streaming.
- At the user's pause on 2026-10-08, the new semantic/native pipeline has not had
  its first native bake/visual approval; the published main catalog is still legacy.
  Existing screenshots must retain their capture dates and cannot represent that new bake.
  Resume composition implementation only when the user requests it.

# Transfer import approval — 2026-10-09

The user explicitly approved the prepared isolated Editor import. Use
`bash tools/import_transfer_editor_isolated.sh --run-editor` for this import:
private user/PID/mount/network namespaces, a fresh namespace-local `/proc`,
masked USB device directory, worker counts 1/4 and the existing nice/ionice.
The probe must pass first. Do not change shared Unity ADB preferences, expose
host USB/network/processes to this run, or launch a player build. This approval
is for transfer AssetDatabase import/audit; it does not activate the prepared
30-node menu/composition or authorize a render/build outside this arrangement.
