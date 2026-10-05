#!/usr/bin/env python3
"""Check real post-link IL2CPP assemblies, not the unstripped Editor DLLs.

Usage: python3 tools/verify_android_ui.py /path/to/UnityProject --unity-editor /path/to/Editor
Requires dotnet and Unity's bundled Mono.Cecil. Checks native C# HTML event
bindings and the legacy JS bridge. This static audit does not prove device UI.
"""
import argparse
from pathlib import Path
import re
import subprocess
import struct
import tempfile
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('project', type=Path)
    parser.add_argument('--unity-editor', type=Path, required=True)
    parser.add_argument('--apk', type=Path, help='APK to audit (defaults to the game release output)')
    parser.add_argument('--gameplay-viewport', action='store_true', help='Also verify the game viewport integration after stripping/AOT.')
    parser.add_argument('--wind-motion', action='store_true', help='Also verify cloth refinement and rain deformation after stripping.')
    parser.add_argument('--rain-surfaces', action='store_true', help='Also verify live-drop contacts and fabric runoff after stripping.')
    parser.add_argument('--camp-landmarks', action='store_true', help='Also verify physical forest trails and readable fire pits after stripping.')
    parser.add_argument('--cozy-vegetation', action='store_true', help='Also verify batched understory and its resource library after stripping.')
    parser.add_argument('--forest-lighting', action='store_true', help='Also verify baked diffuse environment and local canopy scattering after stripping.')
    parser.add_argument('--touch-input', action='store_true', help='Verify typed optional Input System integration after stripping.')
    parser.add_argument('--privacy-content', action='store_true', help='Verify the first-run acknowledgement and remote document loader after stripping.')
    parser.add_argument('--cozy-polish', action='store_true', help='Verify visible groundcover, miniature roadmap and fixed orientation after stripping.')
    args = parser.parse_args()
    managed = args.project.resolve() / 'Library/Bee/artifacts/Android/ManagedStripped'
    cecil = args.unity_editor.resolve() / 'Data/il2cpp/build/deploy/Mono.Cecil.dll'
    if not managed.is_dir() or not cecil.is_file():
        parser.error('Build Android IL2CPP first and provide the installed Unity Editor directory.')
    print('Post-link assemblies: ' + str(managed), flush=True)
    sdk = subprocess.check_output(['dotnet', '--version'], text=True).strip()
    framework = 'net' + sdk.split('.')[0] + '.0'
    from xml.sax.saxutils import escape
    with tempfile.TemporaryDirectory(prefix='quietcamp-ui-linker-') as directory:
        work = Path(directory)
        (work / 'Audit.csproj').write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>' + framework + '</TargetFramework></PropertyGroup>'
            '<ItemGroup><Reference Include="Mono.Cecil"><HintPath>' + escape(str(cecil)) +
            '</HintPath></Reference></ItemGroup></Project>')
        (work / 'Program.cs').write_text(r'''
using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
class Program {
    static int failures;
    static void Check(string directory, string assembly, string name, params string[] methods) {
        var path = Path.Combine(directory, assembly + ".dll");
        if (!File.Exists(path)) {
            Console.WriteLine("FAIL assembly stripped: " + assembly);
            failures++; return;
        }
        using var dll = AssemblyDefinition.ReadAssembly(path);
        var type = dll.MainModule.GetType(name);
        foreach (var method in methods) {
            bool found = type != null && type.Methods.Any(m => m.Name == method && m.HasBody);
            Console.WriteLine((found ? "PASS " : "FAIL ") + name + "." + method);
            if (!found) failures++;
        }
    }
    static int Main(string[] args) {
        var directory = args[0];
        foreach (var item in new[] {
            ("UnityEngine.PhysicsModule", "UnityEngine.MeshCollider"),
            ("UnityEngine.PhysicsModule", "UnityEngine.BoxCollider"),
            ("UnityEngine.PhysicsModule", "UnityEngine.SphereCollider"),
            ("UnityEngine.PhysicsModule", "UnityEngine.CapsuleCollider"),
            ("UnityEngine.CoreModule", "UnityEngine.MeshFilter"),
            ("UnityEngine.CoreModule", "UnityEngine.MeshRenderer") }) {
            using var dll = AssemblyDefinition.ReadAssembly(Path.Combine(directory, item.Item1 + ".dll"));
            bool present = dll.MainModule.GetType(item.Item2) != null;
            Console.WriteLine((present ? "PASS " : "FAIL ") + "Primitive component survives stripping: " + item.Item2);
            if (!present) failures++;
        }
        using (var dll = AssemblyDefinition.ReadAssembly(Path.Combine(directory, "jsb.editor.binding.dll"))) {
            var type = dll.MainModule.GetType("QuickJS.Binding.ReflectBindDelegateGen");
            int actions = type == null ? 0 : type.Methods.Count(m => m.Name == "ActionCall" && m.HasBody);
            int funcs = type == null ? 0 : type.Methods.Count(m => m.Name == "FuncCall" && m.HasBody);
            bool complete = actions == 6 && funcs == 4;
            Console.WriteLine((complete ? "PASS " : "FAIL ") + "QuickJS delegate templates: ActionCall=" + actions + ", FuncCall=" + funcs);
            if (!complete) failures++;
        }
        if (args.Length > 1 && args[1] == "viewport") {
            Check(directory, "Kruty1918.GameplayViewport", "Kruty1918.GameplayViewport.GameplayViewport", "Refresh");
            Check(directory, "Kruty1918.GameplayViewport", "Kruty1918.GameplayViewport.GameplayCameraMath", "TryOrthographic");
            Check(directory, "Kruty1918.GameplayViewport", "Kruty1918.GameplayViewport.GameplayPresentationMath", "TryWorldSpan");
            Check(directory, "Kruty1918.GameplayViewport", "Kruty1918.GameplayViewport.ViewportVisualScale", "ApplyNow", "LateUpdate");
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.TentPresenter", "BindViewport");
        }
        if (args.Skip(1).Contains("wind")) {
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.TentCloth", "Apply", "DeformVertex");
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.FabricGeometry", "Acquire", "Release");
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.CampFoliageResponse", "Apply");
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.RainSurface", "RefreshGeometry", "PreparePose", "Vertex");
        }
        if (args.Skip(1).Contains("rain")) {
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.CampWeather", "CheckDropContacts", "DropVelocity", "EmitDrop");
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.RainSurface", "TryCastSegment", "Slide");
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.RainImpacts", "Advance", "DrawRunoff");
        }
        if (args.Skip(1).Contains("landmarks")) {
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.CampTrail", "BuildAll", "Build", "IsCorridor");
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.CampfireSite", "Create", "ApplyQuality");
            Check(directory, "QuietCamp.Infrastructure", "QuietCamp.Infrastructure.CampContent", "Migrate");
        }
        if (args.Skip(1).Contains("cozy")) {
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.CozyUnderstory", "Build", "Upload", "ApplyQuality", "OnDestroy");
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.CozyVegetationLibrary", "Load");
        }
        if (args.Skip(1).Contains("lighting")) {
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.CanopySunlight", "Configure", "Apply", "Advance", "OnDestroy");
            Check(directory, "QuietCamp.Infrastructure", "QuietCamp.Infrastructure.ForestLightingEnvironment", "Load", "Probe");
        }
        if (args.Skip(1).Contains("touch")) {
            Check(directory, "UnityHTML.InputSystem", "UnityHTML.InputSystem.UnityHtmlInputSystemBackend", "Register", "EnsureModule", "Poll");
            Check(directory, "UnityHTML.Runtime", "UnityHTML.Runtime.UnityHtmlInput", "Ensure", "EnsureEventSystem", "RegisterInputBackend");
            Check(directory, "Unity.InputSystem", "UnityEngine.InputSystem.UI.InputSystemUIInputModule", "Process", "AssignDefaultActions");
        }
        if (args.Skip(1).Contains("polish")) {
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.VisibleForestFloor", "Configure", "RefreshNow", "LateUpdate", "OnDestroy");
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.UI.RoadmapGraphic", "OnPopulateMesh");
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.ScreenOrientationPolicy", "Apply", "Resolve");
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.World.TentCloth", "OnEnable", "Initialize", "OnDestroy");
        }
        if (args.Skip(1).Contains("privacy")) {
            Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.UI.BootPrivacyPanel", "Create", "get_Document", "get_Accepted", "get_Declined");
            Check(directory, "QuietCamp.Application", "QuietCamp.Application.PrivacyAcknowledgement", "IsCurrent", "Record");
            Check(directory, "UnityHTML.Runtime", "UnityHTML.Runtime.Content.UnityHtmlContentLoader", "Load", "ClearCache");
            Check(directory, "UnityHTML.Runtime", "UnityHTML.Runtime.Content.UnityWebContentTransport", "Fetch");
            Check(directory, "UnityHTML.Runtime", "UnityHTML.Runtime.Content.UnityHtmlContentReader", "Decode", "Render");
            Check(directory, "UnityHTML.Runtime", "UnityHTML.Runtime.Content.UnityHtmlContentBinding", "Load", "Cancel", "OpenSource");
        }
        Check(directory, "jsb.core", "QuickJS.Binding.Values", "js_push_classvalue", "js_get_classvalue");
        Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.UI.HtmlCallbacks", "Click", "Number", "Toggle", "ResolveNativeEvent");
        Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.UI.HtmlSurface", "Create", "MountDocument", "MountFailed");
        Check(directory, "QuietCamp.Presentation", "QuietCamp.Presentation.UI.HtmlRecoveryControls", "Show");
        Check(directory, "UnityHTML.Runtime", "UnityHTML.Runtime.UnityHtmlHost", "Mount", "set_NativeEventResolver");
        Check(directory, "ReactUnity", "ReactUnity.Helpers.ReactInterop", "GetType", "GetNamespace", "AddType", "AddNamespace");
        using (var dll = AssemblyDefinition.ReadAssembly(Path.Combine(directory, "QuietCamp.Presentation.dll"))) {
            var callbacks = dll.MainModule.GetType("QuietCamp.Presentation.UI.HtmlCallbacks");
            int bindings = callbacks.NestedTypes.SelectMany(t => t.Methods)
                .Count(m => m.Name.Contains("<ResolveNativeEvent>") && m.HasBody);
            var create = dll.MainModule.GetType("QuietCamp.Presentation.UI.HtmlSurface").Methods.First(m => m.Name == "Create");
            bool native = create.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "set_NativeEventResolver");
            bool complete = bindings == 3 && native;
            Console.WriteLine((complete ? "PASS " : "FAIL ") + "Game native event startup: " + native + ", callback bodies=" + bindings);
            if (!complete) failures++;
        }
        Console.WriteLine(failures == 0 ? "Managed UI post-link audit PASSED" : "Managed UI post-link audit FAILED");
        return failures == 0 ? 0 : 1;
    }
}
''')
        result = subprocess.run(['dotnet', 'run', '--project', str(work / 'Audit.csproj'), '--', str(managed), 'viewport' if args.gameplay_viewport else 'ui', 'wind' if args.wind_motion else 'none', 'rain' if args.rain_surfaces else 'none', 'landmarks' if args.camp_landmarks else 'none', 'cozy' if args.cozy_vegetation else 'none', 'lighting' if args.forest_lighting else 'none', 'touch' if args.touch_input else 'none', 'polish' if args.cozy_polish else 'none', 'privacy' if args.privacy_content else 'none'])
        cpp = args.project.resolve() / 'Library/Bee/artifacts/Android/il2cppOutput/cpp'
        native = {name: set() for name in ('ActionCall', 'FuncCall')}
        pattern = re.compile(r'IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR [^\n;]*ReflectBindDelegateGen_(ActionCall|FuncCall)([^\n(]*)\([^;{]*\)\s*\{')
        for path in [*cpp.glob('GenericMethods*.cpp'), *cpp.glob('jsb.editor.binding*.cpp')]:
            for match in pattern.finditer(path.read_text()):
                native[match[1]].add(match[1] + match[2])
        native_ok = len(native['ActionCall']) >= 6 and len(native['FuncCall']) >= 4
        print(('PASS ' if native_ok else 'FAIL ') + 'Native AOT callback bodies: ' +
              ', '.join(name + '=' + str(len(values)) for name, values in native.items()), flush=True)
        touch_native_ok = True
        if args.touch_input:
            pattern = re.compile(r'IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR [^\n;]*UnityHtmlInputSystemBackend_(Register|EnsureModule|Poll)_[^\n(]*\([^;{]*\)\s*\{')
            methods = set()
            for path in cpp.glob('UnityHTML.InputSystem*.cpp'):
                methods.update(match[1] for match in pattern.finditer(path.read_text()))
            touch_native_ok = methods == {'Register', 'EnsureModule', 'Poll'}
            print(('PASS ' if touch_native_ok else 'FAIL ') + 'Typed Input System native AOT bodies: ' + ', '.join(sorted(methods)), flush=True)
        direct = set()
        pattern = re.compile(r'IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR [^\n;]*U3CResolveNativeEventU3Eb__([012])_[^\n(]*\([^;{]*\)\s*\{')
        for path in cpp.glob('QuietCamp.Presentation*.cpp'):
            direct.update(match[1] for match in pattern.finditer(path.read_text()))
        direct_ok = len(direct) == 3
        print(('PASS ' if direct_ok else 'FAIL ') + 'Native C# click/number/toggle AOT bodies: ' + str(len(direct)), flush=True)
        apk = args.apk or args.project / 'Builds/Android/QuietCamp-MVP-0.1.0.apk'
        print('Android APK: ' + str(apk), flush=True)
        artifact_ok = apk.is_file()
        if artifact_ok:
            with zipfile.ZipFile(apk) as archive:
                libraries = [name for name in archive.namelist() if name.startswith('lib/') and name.endswith('.so')]
                vm_absent = not any(name.endswith('/libquickjs.so') for name in libraries)
                print(('PASS ' if vm_absent else 'FAIL ') + 'Android APK excludes the unused QuickJS VM', flush=True)
                artifact_ok = bool(libraries) and vm_absent
                for name in libraries:
                    elf = archive.read(name)
                    arm64 = elf[:6] == b'\x7fELF\x02\x01' and struct.unpack_from('<H', elf, 18)[0] == 183
                    aligned = False
                    if arm64:
                        offset = struct.unpack_from('<Q', elf, 32)[0]
                        entry_size, count = struct.unpack_from('<HH', elf, 54)
                        loads = [struct.unpack_from('<Q', elf, offset + i * entry_size + 48)[0]
                                 for i in range(count) if struct.unpack_from('<I', elf, offset + i * entry_size)[0] == 1]
                        aligned = bool(loads) and all(value >= 16384 for value in loads)
                    passed = arm64 and aligned
                    print(('PASS ' if passed else 'FAIL ') + name + ' ARM64 / 16 KB ELF alignment', flush=True)
                    artifact_ok &= passed
        else:
            print('FAIL Android APK is missing: ' + str(apk), flush=True)
        passed = result.returncode == 0 and native_ok and touch_native_ok and direct_ok and artifact_ok
        print('Android UI build audit ' + ('PASSED' if passed else 'FAILED'), flush=True)
        raise SystemExit(0 if passed else 1)


if __name__ == '__main__':
    main()
