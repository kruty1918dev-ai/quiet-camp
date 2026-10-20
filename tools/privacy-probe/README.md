# Focused privacy contracts

Run from the repository root with .NET 8:

```sh
dotnet build tools/privacy-probe/Probe.csproj --artifacts-path QuietCamp/Temp/ai/privacy-probe
dotnet QuietCamp/Temp/ai/privacy-probe/bin/Probe/debug/Probe.dll --workers=0 --result=QuietCamp/Temp/ai/privacy-results.xml
```

This runs the actual focused EditMode test sources for Analytics, Crashlytics
consent, rewards, legal configuration, document acknowledgement and offline ads.
Game-domain and privacy implementations are compiled directly. Unity Resources
and JSON loading use file-backed platform doubles; SDK/native Unity behavior is
not emulated or certified by these tests. No Unity Editor, player build, device
or live Firebase project is started.

SDK-adapter doubles are exercised separately by `tools/optional-google-probe`.
Actual DLL/API compilation is checked by `tools/verify_monetization.py --google-sdk-check`
with `QC_UNITY_CACHE_PROJECT` set to a valid Unity compilation cache.
