"""Invoke one native menu item in an explicitly selected, already running Unity Editor."""
import argparse
import ctypes
from ctypes import wintypes

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--pid', type=int, required=True)
parser.add_argument('--menu', required=True)
args = parser.parse_args()
user = ctypes.windll.user32
user.GetMenu.restype = wintypes.HMENU
user.GetSubMenu.restype = wintypes.HMENU
windows = []


@ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
def collect(handle, _):
    pid = wintypes.DWORD()
    user.GetWindowThreadProcessId(handle, ctypes.byref(pid))
    if pid.value == args.pid and user.GetMenu(handle):
        windows.append(handle)
    return True


def find(menu, prefix=''):
    for index in range(user.GetMenuItemCount(menu)):
        label = ctypes.create_unicode_buffer(512)
        user.GetMenuStringW(menu, index, label, 512, 0x400)
        text = label.value.split('\t')[0].replace('&', '')
        path = prefix + text
        child = user.GetSubMenu(menu, index)
        if child:
            yield from find(child, path + '/')
        elif path == args.menu:
            yield user.GetMenuItemID(menu, index)


user.EnumWindows(collect, 0)
matches = [(window, command) for window in windows for command in find(user.GetMenu(window))]
if len(matches) != 1:
    raise SystemExit(f'Expected one matching menu, got {len(matches)}: {args.menu}')
window, command = matches[0]
user.PostMessageW(window, 0x111, command, 0)
print('Requested menu in existing Editor:', args.menu.encode('ascii', 'backslashreplace').decode())
