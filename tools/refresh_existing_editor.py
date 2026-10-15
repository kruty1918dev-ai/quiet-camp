"""Request Refresh only in the already running, explicitly selected Unity PID."""
import argparse
import ctypes
import time
from ctypes import wintypes
p=argparse.ArgumentParser();p.add_argument('--pid',type=int,default=23052);p.add_argument('--focus-only',action='store_true');a=p.parse_args()
u=ctypes.windll.user32;u.GetForegroundWindow.restype=wintypes.HWND
matches=[]
@ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HWND,wintypes.LPARAM)
def collect(handle,_):
 pid=wintypes.DWORD();u.GetWindowThreadProcessId(handle,ctypes.byref(pid));title=ctypes.create_unicode_buffer(512);u.GetWindowTextW(handle,title,512)
 if pid.value==a.pid and u.IsWindowVisible(handle) and title.value.startswith('QuietCamp -'):matches.append(handle)
 return True
u.EnumWindows(collect,0)
assert len(matches)==1,'Expected one visible QuietCamp Editor; no new Editor was started'
if not a.focus_only:
 u.GetMenu.argtypes=[wintypes.HWND];u.GetMenu.restype=wintypes.HMENU
 u.GetSubMenu.argtypes=[wintypes.HMENU,ctypes.c_int];u.GetSubMenu.restype=wintypes.HMENU
 u.GetMenuItemCount.argtypes=[wintypes.HMENU]
 u.GetMenuStringW.argtypes=[wintypes.HMENU,ctypes.c_uint,wintypes.LPWSTR,ctypes.c_int,ctypes.c_uint]
 u.GetMenuItemID.argtypes=[wintypes.HMENU,ctypes.c_int];u.GetMenuItemID.restype=ctypes.c_uint
 menu=u.GetMenu(matches[0]);commands=[]
 for i in range(u.GetMenuItemCount(menu)):
  label=ctypes.create_unicode_buffer(200);u.GetMenuStringW(menu,i,label,200,0x400)
  if label.value.replace('&','')!='Assets':continue
  submenu=u.GetSubMenu(menu,i)
  for j in range(u.GetMenuItemCount(submenu)):
   label=ctypes.create_unicode_buffer(200);u.GetMenuStringW(submenu,j,label,200,0x400)
   if label.value.split('\t')[0].replace('&','')=='Refresh':commands.append(u.GetMenuItemID(submenu,j))
 assert len(commands)==1 and commands[0]!=0xffffffff,'Expected the native Assets/Refresh command'
 u.PostMessageW.argtypes=[wintypes.HWND,wintypes.UINT,wintypes.WPARAM,wintypes.LPARAM]
 assert u.PostMessageW(matches[0],0x111,commands[0],0),'Could not post Refresh to the selected Editor'
 print('Requested native Assets/Refresh in the existing QuietCamp Editor; keyboard focus unchanged')
 raise SystemExit(0)
if u.IsIconic(matches[0]):u.ShowWindow(matches[0],9)
foreground_thread=u.GetWindowThreadProcessId(u.GetForegroundWindow(),None)
current_thread=ctypes.windll.kernel32.GetCurrentThreadId()
attached=foreground_thread!=current_thread and u.AttachThreadInput(current_thread,foreground_thread,True)
try:u.SetForegroundWindow(matches[0])
finally:
 if attached:u.AttachThreadInput(current_thread,foreground_thread,False)
time.sleep(.2)
pid=wintypes.DWORD();u.GetWindowThreadProcessId(u.GetForegroundWindow(),ctypes.byref(pid));assert pid.value==a.pid,'Refused to send keys to another application'
print('Focused existing QuietCamp Editor')
