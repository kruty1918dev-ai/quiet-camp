# Firebase UPM packages

Core, Auth, Analytics and Crashlytics 14.0.0 are included. Download source:
https://developers.google.com/unity/archive . Archive URLs and reproducible
preparation are in `../../tools/prepare_firebase_packages.py`.

Auth, Analytics and Crashlytics are Google's original archives. Core differs only in
`package/package.json`: `com.google.external-dependency-manager: 1.2.190` is
replaced with the existing `com.unity.external-dependency-manager: 2.1.0` used by
AdMob 11.6.0. This avoids duplicate Google resolver assemblies. No SDK binary,
Editor hook, native library or license is modified. The preparation script
compares every other Core member byte-for-byte with Google's archive and checks
the original downloads against pinned SHA-256 values.

Original Google download checksums (measured when downloading 14.0.0):

| Product | SHA-256 |
|---|---|
| Core | `734acab59427b8f51d7aaa326f8a6e9ec61c54b37cbb64ea19eefd1f30e79926` |
| Analytics | `406c9b8efdb34c13f93c8371ec895e59b826d04d09742685dd7818b90eccf544` |
| Crashlytics | `8cd97c49ea5ab5f2133bf1c93178bff3c3568bf9b8df1faa615774c9701112b8` |
| Auth | `c217d4f4c37e1417c67ff6e394c4a5d987272b89e2327a7026a679a89481f884` |

Auth matches the installed Core version to avoid mixing native/C++ SDK versions.
14.1.0 was released on 2026-10-23; this integration keeps the existing 14.0.0
suite together. Its Android Auth dependency is 25.0.0. No additional EDM is added.

These are provenance checks, not a claim that Android Gradle resolution, native
import or device operation has passed. Those checks remain required before
release. See `../../Documentation/FIREBASE-PRIVACY-UA.md`.
