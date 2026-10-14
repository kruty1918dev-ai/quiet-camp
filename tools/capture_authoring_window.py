"""Capture only the visible QuietCamp authoring window in an existing Editor."""
import argparse
import ctypes
import time
from ctypes import wintypes
from pathlib import Path
from PIL import ImageGrab

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--pid', type=int, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
user = ctypes.windll.user32
user.SetProcessDPIAware()
matches = []


@ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
def collect(handle, _):
    pid = wintypes.DWORD()
    user.GetWindowThreadProcessId(handle, ctypes.byref(pid))
    title = ctypes.create_unicode_buffer(512)
    user.GetWindowTextW(handle, title, 512)
    if pid.value == args.pid and user.IsWindowVisible(handle) and 'Майстерня QuietCamp' in title.value:
        matches.append(handle)
    return True


user.EnumWindows(collect, 0)
if len(matches) != 1:
    raise SystemExit(f'Expected one detached authoring window, found {len(matches)}.')
args.output.parent.mkdir(parents=True, exist_ok=True)
handle = matches[0]
user.ShowWindow(handle, 9)
# Keep the detached window visible on the primary monitor before grabbing pixels.
user.MoveWindow(handle, 20, 20, min(1200, user.GetSystemMetrics(0) - 40), min(1000, user.GetSystemMetrics(1) - 80), True)
user.GetForegroundWindow.restype = wintypes.HWND
previous = user.GetForegroundWindow()
foreground_thread = user.GetWindowThreadProcessId(previous, None)
current_thread = ctypes.windll.kernel32.GetCurrentThreadId()
attached = foreground_thread != current_thread and user.AttachThreadInput(current_thread, foreground_thread, True)
try:
    user.SetForegroundWindow(handle)
finally:
    if attached:
        user.AttachThreadInput(current_thread, foreground_thread, False)
time.sleep(.4)
if user.GetForegroundWindow() != handle:
    raise SystemExit('Authoring window is not in the foreground; refused to capture another app.')
rect = wintypes.RECT(); origin = wintypes.POINT()
user.GetClientRect(handle, ctypes.byref(rect)); user.ClientToScreen(handle, ctypes.byref(origin))
capture = ImageGrab.grab(bbox=(origin.x, origin.y, origin.x + rect.right, origin.y + rect.bottom), all_screens=True)
if user.GetForegroundWindow() != handle:
    raise SystemExit('Foreground changed during capture; discarded the frame.')
capture.save(args.output)
print(f'Captured authoring window: {capture.width} x {capture.height}')
