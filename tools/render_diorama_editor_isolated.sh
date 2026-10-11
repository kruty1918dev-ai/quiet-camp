#!/usr/bin/env bash
# User-authorized model review and diorama studies; never a player build.
set -euo pipefail
# Parse the launch body before running the Editor; edits cannot change later shell reads.
main() {
task_root=$(cd "$(dirname "$0")/.." && pwd)
task_editor=/home/oleks/Unity/Hub/Editor/6000.6.2f1/Editor/Unity
case "${1:---probe}" in
  --probe) task_mode=probe ;;
  --models) task_mode=models ;;
  --campaign-models) task_mode=campaign-models ;;
  --studies) task_mode=studies ;;
  --cinematic) task_mode=cinematic ;;
  --pilot-tests) task_mode=pilot-tests ;;
  --deferred-tests) task_mode=deferred-tests ;;
  --campaign-tests) task_mode=campaign-tests ;;
  *) echo 'Use --probe, --models, --campaign-models, --studies, --cinematic or --pilot-tests/--deferred-tests/--campaign-tests'; exit 2 ;;
esac
if [[ "$task_mode" != probe ]]; then
  task_status=$(cat /home/oleks/.local/state/sys-guard/status)
  [[ "$task_status" != *STATE=hard* && "$task_status" != *STATE=crit* ]] || exit 1
  task_available=$(sed -n 's/.*MEM_AVAIL_MB=\([0-9]*\).*/\1/p' <<< "$task_status")
  [[ "$task_available" -ge 1500 ]] || { echo 'Insufficient available RAM'; exit 1; }
  read -r task_disk_available task_disk_size < <(df -B1 --output=avail,size / | tail -1)
  [[ $((task_disk_available*100)) -gt $((task_disk_size*10)) ]] || { echo 'Keep >10% disk free'; exit 1; }
  [[ -x "$task_editor" && -n "${DISPLAY:-}" ]] || { echo 'Editor and X11 display required'; exit 1; }
  if [[ "$task_mode" == cinematic && "${QC_SKIP_REFERENCE_VIDEO:-}" != 1 ]]; then
    export QC_FFMPEG="$(python3 "$task_root/tools/prepare_roadmap_encoder.py")"
  fi
  mkdir -p "$task_root/TestResults"
fi
task_user_mapping=(--map-root-user)
if [[ "$task_mode" == pilot-tests || "$task_mode" == deferred-tests || "$task_mode" == campaign-tests ]]; then task_user_mapping=(--map-current-user --keep-caps); fi
unshare --user "${task_user_mapping[@]}" --pid --fork --mount-proc --net \
  bash -euo pipefail -c '
    if [[ -d /dev/bus/usb ]]; then
      mount -t tmpfs -o mode=755,size=1m tmpfs /dev/bus/usb
      [[ -z "$(ls -A /dev/bus/usb)" ]]
    fi
    [[ "$$" -lt 100 ]]
    [[ $(ip -o link show | wc -l) -eq 1 ]]
    [[ -z "$(ip route show)" ]]
    # Loopback supports namespace-local Unity helpers; no host network route.
    ip link set lo up
    if [[ "$1" == probe ]]; then
      echo "PASS private PID/proc/network + masked USB; graphics uses filesystem X11; no Unity launch"
      exit 0
    fi
    if [[ "$1" == pilot-tests || "$1" == deferred-tests || "$1" == campaign-tests ]]; then
      task_qa=/home/oleks/.cache/quietcamp/cinematic-roadmap-qa
      [[ -d "$task_qa/ProjectSettings" ]]
      for task_cache in ScriptMapper ScriptAssemblies ArtifactDB SourceAssetDB Bee; do
        [[ ! -L "$task_qa/Library/$task_cache" ]] || { echo "QA mutable cache must be private: $task_cache"; exit 1; }
      done
      rg -q "productName: QuietCampRoadmapQA" "$task_qa/ProjectSettings/ProjectSettings.asset"
      # GTK accessibility cannot authenticate to a host bus from the private PID/user namespace.
      # Keep the QA Editor off the desktop accessibility bus as well as the host process/network view.
      unset DBUS_SESSION_BUS_ADDRESS
      export NO_AT_BRIDGE=1
      export QC_CINEMATIC_EVIDENCE_ROOT="${QC_CINEMATIC_EVIDENCE_ROOT:-$3/Design/Roadmap/CinematicPilot/2026-10-10}"
      task_filter=QuietCamp.Tests.RoadmapWorldPlayModeTests
      task_label=cinematic-roadmap
      if [[ "$1" == deferred-tests ]]; then
        task_filter=QuietCamp.Tests.DeferredRoadmapPlayModeTests
        task_label=deferred-roadmap
        export QC_DEFERRED_EVIDENCE_ROOT="$3/Design/Roadmap/Paused/2026-10-11"
      fi
      if [[ "$1" == campaign-tests ]]; then
        task_filter=QuietCamp.Tests.Campaign100CaptureTests
        task_label=campaign100
        export QC_CAMPAIGN100_ROOT="$3/Design/Campaign100/2026-10-11"
      fi
      task_graphics_flags=()
      if [[ "${QC_CAMPAIGN_OFFSCREEN:-}" == 1 ]]; then task_graphics_flags=(-batchmode); fi
      exec setpriv --inh-caps=-all --ambient-caps=-all nice -n10 ionice -c2 -n7 "$2" \
        -force-glcore -noaudio "${task_graphics_flags[@]}" -buildTarget Linux64 -projectPath "$task_qa" \
        -job-worker-count 1 -background-job-worker-count 4 \
        -runTests -testPlatform PlayMode -testFilter "$task_filter" \
        -testResults "$3/TestResults/${task_label}-playmode.xml" \
        -logFile "$3/TestResults/${task_label}-editor.log"
    fi
    if [[ "$1" == models ]]; then
      task_method=QuietCamp.Editor.ModelCatalogueRenderer.Render
    elif [[ "$1" == campaign-models ]]; then
      task_method=QuietCamp.Editor.Campaign100DetailExporter.Export
    elif [[ "$1" == cinematic ]]; then
      task_method=QuietCamp.Editor.CinematicRoadmapCapture.Render
    else
      task_method=QuietCamp.Editor.DioramaStudyRenderer.Render
    fi
    task_burst_flags=()
    # Art publication does not execute Burst gameplay jobs. Avoid regenerating
    # their large cache during every isolated composition review.
    if [[ "$1" == cinematic ]]; then task_burst_flags=(--burst-disable-compilation); fi
    exec nice -n10 ionice -c2 -n7 "$2" \
      -batchmode -force-glcore -buildTarget Linux64 -projectPath "$3/QuietCamp" \
      -job-worker-count 1 -background-job-worker-count 4 \
      "${task_burst_flags[@]}" \
      -executeMethod "$task_method" -quit -logFile "$3/TestResults/diorama-$1-editor.log"
  ' diorama-isolation "$task_mode" "$task_editor" "$task_root"

}
main "$@"
