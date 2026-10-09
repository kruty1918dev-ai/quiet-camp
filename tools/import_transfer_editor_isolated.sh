#!/usr/bin/env bash
# Requires explicit approval under AGENTS.md before --run-editor.
# The namespace probe never launches Unity.
set -euo pipefail
task_root=$(cd "$(dirname "$0")/.." && pwd)
task_editor=/home/oleks/Unity/Hub/Editor/6000.6.2f1/Editor/Unity
case "${1:---probe}" in
  --probe) task_mode=probe ;;
  --run-editor) task_mode=editor ;;
  *) echo 'Use --probe or --run-editor'; exit 2 ;;
esac
if [[ "$task_mode" == editor ]]; then
  task_status=$(cat /home/oleks/.local/state/sys-guard/status)
  [[ "$task_status" != *STATE=hard* && "$task_status" != *STATE=crit* ]] || { echo 'Host guard postpones Unity'; exit 1; }
  task_available=$(sed -n 's/.*MEM_AVAIL_MB=\([0-9]*\).*/\1/p' <<< "$task_status")
  [[ "$task_available" -ge 1500 ]] || { echo 'Insufficient available RAM'; exit 1; }
  task_disk=$(df --output=pcent / | tail -1 | tr -dc '0-9')
  [[ "$task_disk" -lt 90 ]] || { echo 'Keep more than 10% disk free before Editor import'; exit 1; }
  [[ -x "$task_editor" ]] || { echo 'Unity 6000.6.2f1 is missing'; exit 1; }
  mkdir -p "$task_root/TestResults"
fi
unshare --user --map-root-user --pid --fork --mount-proc --net \
  bash -euo pipefail -c '
    # Mask USB only in this private mount namespace. No shared preferences change.
    if [[ -d /dev/bus/usb ]]; then
      mount -t tmpfs -o mode=755,size=1m tmpfs /dev/bus/usb
      [[ -z "$(ls -A /dev/bus/usb)" ]]
    fi
    # No host processes or host network interfaces are visible here.
    [[ "$$" -lt 100 ]]
    [[ $(ip -o link show | wc -l) -eq 1 ]]
    [[ -z "$(ip route show)" ]]
    if [[ "$1" == probe ]]; then
      echo "PASS private PID/proc/network namespaces and masked USB; Unity not launched"
      exit 0
    fi
    exec nice -n10 ionice -c2 -n7 "$2" \
      -batchmode -nographics -buildTarget Linux64 -projectPath "$3/QuietCamp" \
      -job-worker-count 1 -background-job-worker-count 4 \
      -executeMethod QuietCamp.Editor.TransferImportAudit.Run -quit \
      -logFile "$3/TestResults/transfer-import-editor.log"
  ' transfer-isolation "$task_mode" "$task_editor" "$task_root"
