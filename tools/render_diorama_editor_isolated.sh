#!/usr/bin/env bash
# User-authorized model review and diorama studies; never a player build.
set -euo pipefail
task_root=$(cd "$(dirname "$0")/.." && pwd)
task_editor=/home/oleks/Unity/Hub/Editor/6000.6.2f1/Editor/Unity
case "${1:---probe}" in
  --probe) task_mode=probe ;;
  --models) task_mode=models ;;
  --studies) task_mode=studies ;;
  *) echo 'Use --probe, --models or --studies'; exit 2 ;;
esac
if [[ "$task_mode" != probe ]]; then
  task_status=$(cat /home/oleks/.local/state/sys-guard/status)
  [[ "$task_status" != *STATE=hard* && "$task_status" != *STATE=crit* ]] || exit 1
  task_available=$(sed -n 's/.*MEM_AVAIL_MB=\([0-9]*\).*/\1/p' <<< "$task_status")
  [[ "$task_available" -ge 1500 ]] || { echo 'Insufficient available RAM'; exit 1; }
  [[ $(df --output=pcent / | tail -1 | tr -dc '0-9') -lt 90 ]] || { echo 'Keep >10% disk free'; exit 1; }
  [[ -x "$task_editor" && -n "${DISPLAY:-}" ]] || { echo 'Editor and X11 display required'; exit 1; }
  mkdir -p "$task_root/TestResults"
fi
unshare --user --map-root-user --pid --fork --mount-proc --net \
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
    if [[ "$1" == models ]]; then
      task_method=QuietCamp.Editor.ModelCatalogueRenderer.Render
    else
      task_method=QuietCamp.Editor.DioramaStudyRenderer.Render
    fi
    exec nice -n10 ionice -c2 -n7 "$2" \
      -batchmode -force-glcore -buildTarget Linux64 -projectPath "$3/QuietCamp" \
      -job-worker-count 1 -background-job-worker-count 4 \
      -executeMethod "$task_method" -quit -logFile "$3/TestResults/diorama-$1-editor.log"
  ' diorama-isolation "$task_mode" "$task_editor" "$task_root"
