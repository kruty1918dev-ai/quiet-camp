"""Request Refresh only in the already running, explicitly selected Unity PID."""
import argparse
import ctypes
import time
from ctypes import wintypes
p=argparse.ArgumentParser();p.add_argument('--pid',type=int,default=23052);a=p.parse_args()
u=ctypes.windll.user32;u.GetForegroundWindow.restype=wintypes.HWND
matches=[]
@ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HWND,wintypes.LPARAM)
def collect(handle,_):
 pid=wintypes.DWORD();u.GetWindowThreadProcessId(handle,ctypes.byref(pid));title=ctypes.create_unicode_buffer(512);u.GetWindowTextW(handle,title,512)
 if pid.value==a.pid and u.IsWindowVisible(handle) and title.value.startswith('QuietCamp -'):matches.append(handle)
 return True
u.EnumWindows(collect,0)
assert len(matches)==1,'Expected one visible QuietCamp Editor; no new Editor was started'
u.keybd_event(0x12,0,0,0);u.SetForegroundWindow(matches[0]);u.keybd_event(0x12,0,2,0);time.sleep(.2)
pid=wintypes.DWORD();u.GetWindowThreadProcessId(u.GetForegroundWindow(),ctypes.byref(pid));assert pid.value==a.pid,'Refused to send keys to another application'
u.keybd_event(0x11,0,0,0);u.keybd_event(0x52,0,0,0);u.keybd_event(0x52,0,2,0);u.keybd_event(0x11,0,2,0)
print('Requested Refresh in the existing QuietCamp Editor')
