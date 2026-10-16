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

# Diorama design — 2026-10-10

The user requested visual review of every newly imported model, a descriptive
asset catalogue, and 8–12 diorama studies with a Ukrainian atmosphere matching
existing levels. Editor model/study renders use the same approved private
PID/proc/network and masked-USB arrangement. The filesystem X11 socket may be
used for graphics; host network, USB and processes remain isolated. Probe first,
one Unity run at a time, workers 1/4. This request is for dioramas only: do not
create/change puzzles, progression, saves, purchases or publish a new Main
catalog. Preserve source archives. Full vendor model previews stay local.


# Cinematic roadmap implementation — 2026-10-10

The user subsequently approved replacing both old map renderers with the native
continuous Ukrainian valley for QC001–QC005, including pilot launch policy and
menu/gameplay returns. The earlier diorama-only limit is superseded for this work.
Keep full campaign/save/album/entitlement data, existing puzzles and gameplay models.
No tents or story UI on the map; only five waystones and one exit control.
Preserve the user's existing `QC_Renderer.asset` change. Commit/push milestones to main.

The user approved renders and the existing private PID/proc/network/masked-USB
arrangement; player builds remain prohibited. `render_diorama_editor_isolated.sh`
provides `--cinematic` native captures and `--pilot-tests` Game View checks with
separate QA product/save identity and private Bee/ScriptAssemblies/ScriptMapper/
ArtifactDB/SourceAssetDB caches. Never share the compilation Bee cache. Probe first, one Editor at a time, RAM check and
workers 1/4. Native Game View evidence is distinct from Editor geometry estimates
and must never be presented as measured mobile FPS.

The user requested a restrained bird’s-eye overview later the same day. Current
authored camera booms are 128–151 (45% farther), pitch 54–55°, with the same
guided path and pinch bounds. Check native portrait/landscape at maximum zoom-out
after camera changes; surrounding geography carries story without extra UI.

The user also requested self-evident reliable camera navigation. Keep direct drag
tracking the visible world, normalized wheel/trackpad movement and gesture ownership.
Small endpoint inspection margins do not grant reveal or level access. Verify pinch,
OS touch cancellation, tap jitter and both orientations without adding map UI hints.

The user also requested living shoreline water. The roadmap now uses the existing
Stylized Water 3 shader, depth-based bank intersection, four wave layers and baked
progress transparency. Author river settings in CinematicPilot/water.json and rebake;
verify stationary-camera water animation in both URP profiles alongside Game View
flow. Preserve the original QC_Renderer.asset bytes throughout Editor checks.

# Roadmap deferred — 2026-10-11

The user requested preserving the roadmap while excluding it completely from
players and postponing its development. Map-only native assets, shaders, cultural
models, catalogs and CSS live under `Assets/QuietCamp/Editor/RoadmapArchive`,
outside every Resources folder. Native world/input types are UNITY_EDITOR-only.
Keep default Editor play and all players on direct campaign navigation; no map
card or map-return destination. The five-place launch restriction applies only
to an explicitly opened archived Editor preview. Campaign, saves, gameplay
models, album and entitlement rules remain intact. Never republish archived map
assets to Resources during routine content preparation. The build dependency
guard rejects map assets reintroduced into player roots. This does not authorize
a player build. Existing Unity isolation, RAM and renderer-preservation rules apply.

# Campaign 100 — 2026-10-11

The user authorized redesigning the base game as exactly 100 levels, retaining
historical saves and supporting future content packs. Roadmap deferral still
applies. Current sources and acceptance work live in
`Design/Campaign100/2026-10-11`; start with `PLAN-UA.md`, `MECHANICS-UA.md` and
`STORY-UA.md`. `Authoring/places.tsv` and `tools/build_campaign100_design.py`
prepare level drafts; `Campaign100Authoring` validates every puzzle before
publishing Resources. Preserve original IDs and the 274 historical content
revisions; archived journeys/bonus must not inflate the base campaign.

Use the autonomous .NET probe for validator/witness/independent solver,
content-pack installation and save/session contracts. It restores Newtonsoft
through NuGet, independently of Unity Library. Cached compiler checks still
need real Unity response files. Neither check proves visual acceptance.

Native campaign captures use `render_diorama_editor_isolated.sh --campaign-tests`
in the same approved isolation. `QC_CAMPAIGN_OFFSCREEN=1` captures the actual
world components through a native URP camera without allocating the menu/HUD;
it is world-camera evidence, not Game View UI-flow evidence. All capture
receipts must match the published level content hash, PNG SHA-256 and current
`capture-source-manifest.json` revision. Prepare that manifest with
`python3 tools/campaign100_capture_sources.py` before capturing; do not change
captured sources during a run. The report labels valid older frames separately.
`--campaign-edit-tests` checks campaign/archived-map EditMode contracts;
`--campaign-flow-tests` checks the direct campaign Game View flow. Neither may
run concurrently with another Unity job. A partial capture or stale NUnit XML
must not be advertised as a newly passed full run. Full vendor-model
photographs remain local. Historical captures keep their dates. One Editor,
RAM/disk checks, workers 1/4 and the renderer-preservation rule remain required.

# First-ten after captures — 2026-10-12

The user requested native after renders for QC001–QC010 and explicitly said
not to close Chrome. This resumes the previously deferred Unity capture work.
Keep Chrome open; do not interfere with other desktop or phone sessions.
Use the existing isolated campaign capture wrapper, one Editor at a time,
RAM/disk checks and workers 1/4. QA compilation caches remain private.
First-ten evidence uses QC_FIRST_TEN_EVIDENCE=1 and is stored separately in
Design/Campaign100/2026-10-11/FirstTenRefinement/After. Short serial batches
avoid accumulating Editor memory; preserve individual XML results. These
are native world-camera frames without HUD, not mobile FPS or full-plan
acceptance. Preserve historical baseline and the user's QC_Renderer.asset.
Player builds and roadmap activation remain prohibited.

# First-ten rollback — 2026-10-12

After reviewing the experimental first-ten captures, the user requested restoring
the previous implementation. QC001–QC010, campaign and summaries use the exact
pre-publication baseline from cc308798. FirstTenRefinement/rollback-receipt.json
records the restoration. Experimental sources, packages, bakes and captures remain
under Design; experimental BakedEnvironments were removed from Resources.
Do not republish this experiment or resume the refinement plan without a new user
request. The roadmap remains deferred; the campaign remains exactly 100 levels.
Chrome must remain open during any future authorized capture work.

# WorldOps visual workflow — 2026-10-16

Правки оформлення рівнів виконувати через WorldOps. Інструмент керує підготовкою,
перевірками, рендерами, повторними виправленнями та застосуванням. Агент оцінює
актуальні кадри й повертає структурований висновок. Приймання визначається
виконанням критеріїв; незастарілі докази обов'язкові. Згенеровані JSON, хеші та
квитанції вручну не виправляти.

Use `tools/qc_world.py --project <existing-QA-project> run <recipe.json>` and
the same service in the workshop's WorldOps section. See
`Design/LevelAuthoring/WorldOps.md`. Recipes belong in
`QuietCamp/Assets/QuietCamp/Authoring/WorldOps/`; editor implementation is the
only editing authority. Extend an allow-listed operation and its checks when
a capability is missing, then use the shared process. Do not add another ad-hoc
Python writer or use historical scripts to bypass it.

Inspect current before/after Game View frames with active UI in both profiles;
submit a `ReviewVerdict` bound to their hashes. Diagnostic masks are not gameplay
renders. No review agent means awaiting review, never artistic acceptance.
At most five correction rounds per level; the same unchanging defect twice
becomes an exception. Fix criteria, not cycle counts. The old seven-cycle rule
is superseded for this workflow. Record defects, corrections and residuals.
Only explicitly requested agent delivery commits/pushes owned files; ordinary
workshop actions never run Git. Existing continuous delivery still applies to
the agent's verified implementation milestones.

Review river shape, terrain/water contact, shoreline ecology, repeated silhouettes,
flower and fence visibility, foreground framing, lighting, shadows and artifacts.
Consult relevant primary documentation when behavior is uncertain. Numerical
generator scores cannot replace visual review or prove reference equivalence.
Use actual Unity captures as implementation evidence. Keep source revisions stable
during each capture batch. Preserve gameplay, saves, the existing renderer assets
and the Unity/process restrictions above. Never claim an unresolved frame passed.

# Scene narrative and contact

For scene work, follow Design/LevelAuthoring/SCENE-STORY-RULES-UA.md: physical supports, material-specific baked weathering, actual PWB placement, purposeful human traces and game-camera review. Preserve the common story across seasons; inspect both overview and details before acceptance.
