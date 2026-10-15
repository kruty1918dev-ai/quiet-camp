"""Focus only the existing floating Unity Game View, never another app or a new Editor."""
import argparse
import ctypes
from ctypes import wintypes
import time

p=argparse.ArgumentParser(description=__doc__);p.add_argument('--pid',type=int,required=True);p.add_argument('--allow-docked',action='store_true',help='Caller already focused the docked Game View through Unity.');args=p.parse_args()
u=ctypes.windll.user32;u.GetForegroundWindow.restype=wintypes.HWND
matches=[];main=[]
@ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HWND,wintypes.LPARAM)
def collect(handle,_):
    pid=wintypes.DWORD();u.GetWindowThreadProcessId(handle,ctypes.byref(pid))
    title=ctypes.create_unicode_buffer(512);u.GetWindowTextW(handle,title,512)
    if pid.value==args.pid and u.IsWindowVisible(handle) and title.value=='Game':matches.append(handle)
    if pid.value==args.pid and u.IsWindowVisible(handle) and title.value.startswith('QuietCamp -'):main.append(handle)
    return True
u.EnumWindows(collect,0)
if not matches and args.allow_docked and len(main)==1:matches=main
if len(matches)!=1:raise SystemExit('Expected exactly one existing floating Game View; no focus change made.')
handle=matches[0]
if u.GetForegroundWindow()!=handle:
    if u.IsIconic(handle):u.ShowWindow(handle,9)
    current=ctypes.windll.kernel32.GetCurrentThreadId()
    foreground=u.GetWindowThreadProcessId(u.GetForegroundWindow(),None)
    attached=foreground!=current and u.AttachThreadInput(current,foreground,True)
    try:u.SetForegroundWindow(handle)
    finally:
        if attached:u.AttachThreadInput(current,foreground,False)
    time.sleep(.2)
if u.GetForegroundWindow()!=handle:raise SystemExit('Existing Game View could not receive focus.')
print('Focused the existing Editor Game View before capture.')
