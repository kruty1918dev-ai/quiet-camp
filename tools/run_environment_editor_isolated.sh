#!/usr/bin/env bash
# Standalone UPM EditMode checks in the already-approved phone-safe isolation.
# Never invokes BuildPipeline, a player, Android SDK, or shared Editor prefs.
set -euo pipefail
task_root=$(cd "$(dirname "$0")/.." && pwd)
task_editor=/home/oleks/Unity/Hub/Editor/6000.6.2f1/Editor/Unity
task_mode=${1:---probe}
if [[ "$task_mode" != --probe && "$task_mode" != --project ]]; then echo 'Use --probe or --project <prepared-clean-project> <result-prefix>'; exit 2; fi
if [[ "$task_mode" == --project ]]; then
  [[ $# == 3 ]] || exit 2
  task_project=$(realpath "$2")
  [[ "$task_project" == /home/oleks/.cache/quietcamp/environment-tools-* && -f "$task_project/environment-tools-clean-project.json" ]] || { echo 'Use an owned prepared clean project'; exit 1; }
  task_result=$(realpath -m "$3")
  [[ "$task_result" == "$task_root/TestResults/"* || "$task_result" == "$task_root/Design/Campaign100/2026-10-11/FirstTenRefinement/"* ]] || exit 1
  task_status=$(cat /home/oleks/.local/state/sys-guard/status)
  task_available=$(sed -n 's/.*MEM_AVAIL_MB=\([0-9]*\).*/\1/p' <<< "$task_status")
  [[ "$task_available" -ge 1500 && "$task_status" != *STATE=hard* && "$task_status" != *STATE=crit* ]] || { echo 'Require 1500 MB RAM and safe host state'; exit 1; }
  read -r task_free task_size < <(df -B1 --output=avail,size / | tail -1)
  [[ $((task_free*100)) -gt $((task_size*10)) ]] || { echo 'Keep >10% disk free'; exit 1; }
  if pgrep -x Unity >/dev/null; then echo 'Another Unity Editor is running; only one allowed'; exit 1; fi
  mkdir -p "$(dirname "$task_result")"
else
  task_project=;task_result=
fi
unshare --user --map-current-user --keep-caps --pid --fork --mount-proc --net \
  bash -euo pipefail -c '
    if [[ -d /dev/bus/usb ]]; then mount -t tmpfs -o mode=755,size=1m tmpfs /dev/bus/usb; [[ -z "$(ls -A /dev/bus/usb)" ]]; fi
    [[ "$$" -lt 100 && $(ip -o link show | wc -l) -eq 1 && -z "$(ip route show)" ]]
    ip link set lo up
    if [[ "$1" == --probe ]]; then echo "PASS private PID/proc/network and masked USB; no Editor"; exit 0; fi
    unset DBUS_SESSION_BUS_ADDRESS; export NO_AT_BRIDGE=1
    exec setpriv --inh-caps=-all --ambient-caps=-all nice -n10 ionice -c2 -n7 "$2" \
      -batchmode -nographics -noaudio -projectPath "$3" \
      -job-worker-count 1 -background-job-worker-count 4 \
      -runTests -testPlatform EditMode -testResults "$4.xml" -logFile "$4.log"
  ' environment-tools-isolation "$task_mode" "$task_editor" "$task_project" "$task_result"
