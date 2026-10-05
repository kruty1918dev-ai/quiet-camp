#!/usr/bin/env python3
"""Exercise menu/settings using real ADB touch input in the isolated .uiqa APK.

The passive tools/qa/MenuDeviceProbe.cs fixture reports actual Unity control
positions and values. It belongs in the isolated build project only. The script
never clears app data, accepts optional analytics, or confirms game-data deletion.
"""
import argparse
import json
import pathlib
import re
import subprocess
import time

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--serial', required=True)
parser.add_argument('--output', type=pathlib.Path, required=True)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
package = 'com.kruty1918.quietcamp.uiqa'
checks = []


def adb(*items, binary=False):
    return subprocess.check_output(['adb', '-s', args.serial, *map(str, items)], text=not binary, timeout=20)


def snapshot():
    pid = adb('shell', 'pidof', package).strip()
    raw = adb('logcat', '--pid=' + pid, '-d', '-v', 'raw', 'Unity:I', '*:S')
    # Unity splits long Android log messages at the logcat payload limit. A
    # roadmap with all 30 controls can span several consecutive raw records.
    lines = raw.splitlines()
    frames = {}
    latest = None
    for line in lines:
        chunk = re.search(r'\[MenuDeviceQA:(\d+):(\d+):(\d+)\] (.*)', line)
        if not chunk:
            continue
        frame, part, count = map(int, chunk.group(1, 2, 3))
        pieces = frames.setdefault(frame, {})
        pieces[part] = chunk.group(4)
        if len(pieces) == count and all(i in pieces for i in range(count)):
            try:
                latest = json.loads(''.join(pieces[i] for i in range(count)))
            except json.JSONDecodeError:
                continue
    if latest is not None:
        return latest
    for index in reversed(range(len(lines))):
        if '[MenuDeviceQA] ' not in lines[index]:
            continue
        payload = lines[index].split('[MenuDeviceQA] ', 1)[1]
        for part in range(index, min(index + 10, len(lines))):
            if part > index:
                if '[MenuDeviceQA] ' in lines[part]:
                    break
                payload += lines[part]
            try:
                return json.loads(payload)
            except json.JSONDecodeError:
                continue
    return {}


def controls(state):
    return {c['id']: c for c in state.get('controls', [])}


def wait(predicate, label, timeout=30):
    deadline = time.monotonic() + timeout
    last = {}
    while time.monotonic() < deadline:
        try:
            last = snapshot()
            if predicate(last):
                checks.append({'check': label, 'result': 'passed'})
                print('PASS ' + label, flush=True)
                return last
        except (subprocess.CalledProcessError, json.JSONDecodeError):
            pass
        time.sleep(.3)
    (args.output / 'failure-state.json').write_text(json.dumps(last, indent=2, ensure_ascii=False))
    raise AssertionError(label + ': expected UI state did not appear')


def visible(id):
    return wait(lambda s: controls(s).get(id, {}).get('visible', False), 'visible ' + id)


def tap(id):
    state = visible(id)
    button = controls(state)[id]
    assert button['enabled'], 'disabled ' + id
    adb('shell', 'input', 'tap', button['x'], button['y'])
    time.sleep(.65)


def reveal(id):
    for _ in range(9):
        state = snapshot()
        control = controls(state).get(id)
        assert control is not None, 'missing ' + id
        if control['visible']:
            return
        # Scroll inside the detail pane; on wide layouts leave the sidebar alone.
        w, h = state['width'], state['height']
        down = control['y'] > h * .5
        adb('shell', 'input', 'swipe', round(w * .7), round(h * (.73 if down else .32)),
            round(w * .7), round(h * (.32 if down else .73)), 450)
        time.sleep(.65)
    raise AssertionError('Cannot scroll to ' + id)


def shot(name):
    (args.output / (name + '.png')).write_bytes(adb('exec-out', 'screencap', '-p', binary=True))
    (args.output / (name + '.json')).write_text(json.dumps(snapshot(), indent=2, ensure_ascii=False))


def has(*ids):
    return lambda state: all(i in controls(state) for i in ids)


def slider(id, fraction):
    reveal(id)
    c = controls(snapshot())[id]
    adb('shell', 'input', 'swipe', c['x'], c['y'], round(c['x'] + c['width'] * (fraction - .5)), c['y'], 500)
    time.sleep(.7)


def launch():
    adb('shell', 'am', 'force-stop', package)
    adb('shell', 'monkey', '-p', package, '-c', 'android.intent.category.LAUNCHER', '1')
    return wait(lambda s: (has('continue')(s) and s.get('startupReady')) or has('boot-policy-ack')(s), 'launch has actionable UI', timeout=100)


def acknowledge_if_pending():
    state = wait(lambda s: has('continue')(s) or has('boot-policy-ack')(s), 'menu or privacy disclosure', timeout=100)
    if has('boot-policy-ack')(state):
        assert not state.get('startupReady'), 'game started before policy acknowledgement'
        wait(lambda s: controls(s).get('boot-policy-ack', {}).get('enabled', False), 'policy document loaded before acknowledgement')
        tap('boot-policy-ack')
    return wait(lambda s: has('continue', 'settings', 'levels', 'album')(s) and s.get('startupReady'), 'boot to usable menu', timeout=100)


def return_to_main(label):
    state = wait(lambda s: s.get("scene") == "MainMenu" and (has("continue")(s) or has("back")(s)), label, timeout=60)
    if not has("continue")(state):
        tap("back")
    return wait(has("continue", "levels", "album"), "main menu restored")


def run():
    state = launch()
    if has('boot-policy-ack')(state):
        assert state['privacyPending'] and not state['startupReady']
        assert not controls(state)['boot-policy-source']['enabled']
        shot('00-privacy-draft')
        tap('boot-policy-full'); shot('00-privacy-full')
        adb('shell', 'input', 'swipe', round(state['width']*.5), round(state['height']*.62), round(state['width']*.5), round(state['height']*.36), 600)
        time.sleep(.6); shot('00-privacy-scrolled')
        tap('boot-policy-exit')
        deadline = time.monotonic() + 12
        closed = False
        while time.monotonic() < deadline:
            try:
                adb('shell', 'pidof', package)
            except subprocess.CalledProcessError:
                closed = True; break
            time.sleep(.3)
        assert closed, 'refusal did not exit the Android player'
        checks.append({'check':'privacy refusal exits player', 'result':'passed'})
        state = launch()
        assert has('boot-policy-ack')(state), 'refusal was incorrectly persisted as acceptance'
        checks.append({'check':'privacy shown again after refusal', 'result':'passed'})
    state = acknowledge_if_pending()
    assert state.get('privacyAcknowledged')
    original = {key: state[key] for key in ['language', 'master', 'orientation', 'textScale', 'haptics']}
    if state.get('orientation') != 0:
        tap('settings'); tap('set-cat-look'); reveal('orientation-0'); tap('orientation-0')
        wait(lambda s: s.get('height', 0) > s.get('width', 1), 'portrait baseline for repeated QA')
        tap('settings-close'); wait(has('continue'), 'main menu after portrait baseline')
    shot('01-menu-phone')
    tap('settings'); wait(has('set-cat-sound', 'set-cat-comfort', 'set-cat-look', 'language-uk'), 'settings overview')
    shot('02-settings-phone')
    tap('set-cat-sound'); wait(has('settings.master', 'settings.music'), 'sound page')
    slider('settings.master', .3)
    wait(lambda s: .1 < s.get('master', 1) < .65, 'master volume responds to drag')
    slider('settings.master', original['master'])
    tap('back'); wait(has('set-cat-comfort'), 'back to overview')
    tap('set-cat-comfort'); wait(has('settings.haptics', 'settings.textSize'), 'comfort page')
    reveal('settings.haptics'); tap('settings.haptics')
    wait(lambda s: s.get('haptics') != original['haptics'], 'haptics responds to touch')
    tap('settings.haptics'); wait(lambda s: s.get('haptics') == original['haptics'], 'haptics restores')
    slider('settings.textSize', 1)
    wait(lambda s: s.get('textScale', 0) > 1.28, '130 percent text')
    tap('back')
    for lang in ['uk', 'en', 'de']:
        reveal('language-' + lang); tap('language-' + lang)
        wait(lambda s: s.get('language') == lang, 'language ' + lang)
        shot('03-settings-' + lang + '130')
        tap('set-cat-privacy'); wait(has('privacy-section-data', 'privacy-section-analytics', 'privacy-section-documents'), 'privacy overview ' + lang)
        shot('04-privacy-' + lang + '130')
        tap('privacy-section-documents'); wait(has('privacy-policy', 'legal-terms', 'legal-support'), 'documents page ' + lang)
        state = snapshot()
        for id in ['privacy-policy', 'legal-terms', 'legal-support']:
            assert not controls(state)[id]['enabled'], 'unpublished legal link enabled: ' + id
        shot('05-documents-' + lang + '130')
        tap('back'); wait(has('privacy-section-analytics'), 'back from documents')
        tap('privacy-section-analytics'); wait(has('analytics-consent', 'analytics-clear'), 'analytics page')
        assert not controls(snapshot())['analytics-consent']['enabled'], 'analytics without SDK must be disabled'
        tap('back'); wait(has('privacy-section-data'), 'back from analytics')
        tap('privacy-section-data'); wait(has('legal-data-information', 'local-data-erase'), 'data page')
        reveal('legal-data-information'); tap('legal-data-information')
        shot('06-information-' + lang + '130')
        tap('back'); wait(has('privacy-section-data'), 'back from data')
        tap('back'); wait(has('language-uk'), 'privacy back to overview')
    tap('set-cat-look'); wait(has('orientation-1'), 'display page')
    reveal('orientation-1'); tap('orientation-1')
    wait(lambda s: s.get('width', 0) > s.get('height', 1) and s.get('orientation') == 1, 'landscape orientation')
    shot('07-display-landscape-de130')
    tap('back'); shot('08-settings-landscape-de130')
    tap('set-cat-privacy'); shot('09-privacy-landscape-de130')
    tap('settings-close'); wait(has('continue', 'settings'), 'close directly to main menu')
    shot('10-menu-landscape-de130')
    tap('levels'); wait(has('back'), 'roadmap opens')
    shot('11-roadmap-landscape')
    state = snapshot(); adb('shell', 'input', 'swipe', round(state['width'] * .72), round(state['height'] * .76), round(state['width'] * .72), round(state['height'] * .3), 600)
    time.sleep(.8); shot('12-roadmap-scrolled')
    tap('back'); wait(has('continue'), 'roadmap back to menu')
    tap('album'); wait(has('back'), 'album opens'); shot('13-album-landscape')
    tap('back'); wait(has('continue'), 'album back to menu')
    tap('settings'); tap('set-cat-look'); reveal('orientation-0'); tap('orientation-0')
    wait(lambda s: s.get('height', 0) > s.get('width', 1), 'portrait orientation restored')
    tap('settings-close')
    first_pid = adb('shell', 'pidof', package).strip()
    first_log = adb('logcat', '--pid=' + first_pid, '-d', '-v', 'threadtime', 'Unity:I', 'AndroidRuntime:E', '*:S')
    (args.output / 'logcat-initial.txt').write_text(first_log)
    assert 'NullReferenceException' not in first_log and 'FATAL EXCEPTION' not in first_log and '[QuietCamp HTML]' not in first_log
    # Persistence uses force-stop/relaunch, never app-data deletion.
    launch(); acknowledge_if_pending()
    wait(lambda s: has('continue')(s) and s.get('language') == 'de' and s.get('textScale', 0) > 1.28, 'preferences survive restart', timeout=100)
    state = launch()
    assert has('continue')(state), 'unchanged acknowledged policy prompted again'
    checks.append({'check':'unchanged acknowledgement skips next launch', 'result':'passed'})
    tap('continue'); wait(lambda s: s.get('scene') == 'Camp' and has('pause')(s) and s.get('gameplayActive'), 'touch starts playable camp', timeout=60)
    shot('14-gameplay-phone-de130')
    tap('pause'); wait(has('settings', 'resume'), 'pause opens'); tap('settings')
    tap('set-cat-privacy'); tap('privacy-section-data')
    assert 'local-data-erase' not in controls(snapshot()), 'erase action exposed during gameplay'
    tap('settings-close'); wait(has('resume'), 'settings returns to pause')
    tap('resume'); wait(lambda s: has('pause')(s) and s.get('gameplayActive'), 'game resumes')
    shot('15-gameplay-resumed')
    # Repeated runs may restore a later glade. Keep all command-count tests on
    # the same introductory content without deleting the isolated save.
    tap('pause'); tap('menu')
    return_to_main('menu before introductory replay')
    tap('levels'); reveal('level-0'); tap('level-0')
    wait(lambda s: s.get('scene') == 'Camp' and has('pause')(s) and s.get('gameplayActive'), 'introductory glade for placement QA', timeout=60)
    state = snapshot(); before = state['placements']; revision = state['boardRevision']
    if 'guest-card' in controls(state):
        tap('guest-card')
        state = wait(lambda s: bool(s.get('placementTargets')), 'board has valid placement target')
        target = state['placementTargets'][0]
        adb('shell', 'input', 'tap', target['x'], target['y'])
        wait(lambda s: s.get('placements') == before + 1 and s.get('boardRevision') == revision + 1, 'tap places exactly one tent')
        shot('16-tent-placed'); tap('undo')
        wait(lambda s: s.get('placements') == before and s.get('canRedo'), 'undo restores layout')
        tap('redo'); wait(lambda s: s.get('placements') == before + 1, 'redo restores tent')
        tap('undo'); wait(lambda s: s.get('placements') == before, 'undo prepares card drag')
        state = snapshot(); revision = state['boardRevision']; card = controls(state)['guest-card']; target = state['placementTargets'][0]
        adb('shell', 'input', 'swipe', card['x'], card['y'], target['x'], target['y'], 1000)
        wait(lambda s: s.get('placements') == before + 1 and s.get('boardRevision') == revision + 1, 'card drag creates exactly one command')
        shot('17-card-drag-placed'); tap('undo'); wait(lambda s: s.get('placements') == before, 'undo after drag')
        state = snapshot(); revision = state['boardRevision']; card = controls(state)['guest-card']
        adb('shell', 'input', 'swipe', card['x'], card['y'], round(state['width']*.5), 30, 1000)
        time.sleep(1)
        assert snapshot()['boardRevision'] == revision and snapshot()['placements'] == before, 'invalid card drop changed layout'
        checks.append({'check':'invalid card drop preserves layout', 'result':'passed'})
    # Finish the two-guest introductory glade through the same public touch UI.
    # The probe only previews targets; it never commits or solves the board.
    for count in range(snapshot()['placements'], 2):
        tap('guest-card')
        state = wait(lambda s: bool(s.get('placementTargets')), 'completion placement target')
        target = state['placementTargets'][0]
        adb('shell', 'input', 'tap', target['x'], target['y'])
        wait(lambda s: s.get('placements') == count + 1, 'completion tent ' + str(count + 1))
    tap('check'); wait(has('next'), 'check completes introductory glade')
    shot('18-level-completed')
    tap('next')
    wait(lambda s: has('pause')(s) and not has('next')(s) and s.get('gameplayActive') and s.get('placements') == 0, 'next glade becomes playable', timeout=60)
    tap('pause'); tap('menu')
    return_to_main('pause returns to main menu')
    tap('album'); wait(has('album-0', 'album-replay'), 'completed camp appears in album')
    time.sleep(1.5); shot('19-album-populated')
    state = snapshot()
    adb('shell', 'input', 'swipe', round(state['width']*.35), round(state['height']*.42),
        round(state['width']*.65), round(state['height']*.42), 800)
    time.sleep(.8); shot('20-album-rotated')
    tap('album-replay')
    wait(lambda s: s.get('scene') == 'Camp' and has('pause')(s) and s.get('gameplayActive'), 'album replay opens playable camp', timeout=60)
    tap('pause'); tap('settings'); tap('set-cat-comfort'); slider('settings.textSize', (original['textScale'] - .85) / .45)
    tap('back'); reveal('language-' + original['language']); tap('language-' + original['language'])
    tap('settings-close'); tap('resume')
    # Scope all diagnostics to this app's process.
    pid = adb('shell', 'pidof', package).strip()
    log = adb('logcat', '--pid=' + pid, '-d', '-v', 'threadtime', 'Unity:I', 'AndroidRuntime:E', '*:S')
    (args.output / 'logcat.txt').write_text(log)
    assert 'NullReferenceException' not in log and 'FATAL EXCEPTION' not in log and '[QuietCamp HTML]' not in log
    checks.append({'check': 'no Unity HTML or null-reference crash', 'result': 'passed'})

try:
    run()
except Exception as error:
    checks.append({'check': str(error), 'result': 'failed'})
    try:
        shot('failure')
    except Exception:
        pass
    raise
finally:
    (args.output / 'checks.json').write_text(json.dumps(checks, indent=2, ensure_ascii=False))
