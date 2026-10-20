# Quiet Camp privacy release candidate — 20 October 2026

Prepared from public main 01784d3, preserving the local canonical-link commit.
This is source preparation, not certification of an Android release. No Unity
Editor, Android/player build, install, phone/ADB operation or remote push ran.

## Implemented

- Local age bands: Unknown, under 13, 13–15, 16–17 and adult. No exact birth date.
  Old saves without a current age choice do not activate optional providers.
- Boot requires an age band of at least 13 to continue. Settings exposes the
  band and explains restrictions. Selecting a different band clears optional
  permissions; selecting the current band preserves them.
- Core Analytics consent/availability is additionally age-gated. The existing
  six numeric qc_* event families and level 1–30 measurement limit are preserved.
- Independent CrashReportConsent and optional Android Firebase transport,
  separate from Analytics. SDK detection recognizes Crashlytics and the new
  Google age-treatment enum. No new user IDs, custom crash logs or keys.
- Every ad provider is wrapped by AgeRestrictedAdService. Its factory is not
  called for unknown/under-16 age, including sample/development providers.
- A separate optional-ad permission is required before UMP network refresh or
  ad readiness, including development sample inventory. The publisher path
  refreshes UMP on eligible launches and retries failed updates on ad request.
- All ad loads use npa=1. Ages 16–17 receive AgeRestrictedTreatment.Teen;
  adults Unspecified, still with NPA. PG is the selected maximum ad content rating.
- CanRequestAds alone is not treated as device-storage consent. For TCF-required
  publisher requests, conservative additional checks require purpose 1 and
  Google vendor 755 consent in the SDK-managed IAB preferences. The exact GVL
  mapping, account vendor selection and resulting ad behavior need release QA.
  This is not an independent CMP or a complete legal TCF-purpose validator.
- Android library manifest removes FirebaseInitProvider, defaults Firebase and
  Crashlytics collection off, retains denied Analytics/ad-storage defaults and
  delays Mobile Ads app measurement. Final manifest/runtime effects are pending.
- Complete en/uk/de privacy controls, candidate bundled notice and current
  canonical privacy/data-rights links. Existing acknowledgement grants no consent.

## Explicitly not activated

legal.publishedOn is empty; teenPrivacyReviewed and sdkDisclosuresReviewed are
false. Analytics is disabled. Advertising uses development sample inventory
only; production publisher IDs are empty. crashlyticsEnabled and the additional
crashlyticsRuntimeVerified gate remain false. Do not flip these as a substitute
for the missing SDK/console/runtime evidence.

## Critical installed-SDK verification

The developer confirms installing Firebase Analytics and Crashlytics in a newer
local project, but only the older committed source is available here. Conditional
adapter compilation against contract doubles does not verify that installation.

Crashlytics automatic-collection overrides can persist across process death.
Disabling defaults in a manifest is not proof that a previous override is gone.
Verify cold startup, old-release caches, Android providers and Unity SDK startup
hooks. Analytics-only opt-in must not re-enable Crashlytics; crash-only opt-in
must not enable Analytics. SDK-local diagnostic storage must also be assessed,
not only upload. The Android deleteUnsentReports call queues deletion; its timing
relative to enablement and next-launch upload is NOT verified by this patch.
If these checks fail, keep Crashlytics gated while the actual integration is fixed.

For advertisements verify the real published UMP message, vendor list, applicable
TCF choices, NPA/Teen signals, refusal, withdrawal and unknown age. Setting npa=1
is not proof that no personal data is processed or that consent is unnecessary.
No native/privacy behavior is claimed to pass based on the source gate alone.

Backups: final Android allowBackup/dataExtractionRules/fullBackupContent behavior
remains unverified. Do not promise that app deletion also erases OS backups or
that a restored privacy choice is automatically valid for a different revision.

## Verification actually performed

Run the dependency-free .NET 10 contract probe (not a Unity/player build):

    dotnet run --project tools/PrivacyProbe/PrivacyProbe.csproj -p:UseSharedCompilation=false

It compiles the real new core and conditional SDK adapters against explicitly
labelled SDK/platform doubles. Tests cover age gating, no provider initialization
before eligibility, independent consent, withdrawal races, stale revisions,
legacy saves, NPA/Teen request configuration, storage/vendor refusal, no ads
without ad permission, and SDK-adapter separation. These tests are not Android
traffic, real SDK compatibility, scene hookup or native UI evidence.

Existing Editor/PlayMode fixture sources are updated for explicit adult age
selection. They were not run; no Unity cache is supplied in this environment.
A real test artifact requires the owner's existing artifact or separate build
authorization, consistent with root AGENTS.md.

## Integration and release sequence

1. Apply/reconcile this patch in the latest developer project, preserving any newer
   SDK work. Re-run contracts and compile with the actual Unity/Google SDKs.
2. Verify native startup and the complete age/consent/network/backup matrix on an
   identified Android artifact. Retain version/hash, settings and test evidence.
3. Complete Google/GA4/AdMob account, vendor, retention and transfer reviews and
   mail/host deletion procedures using the website's release-evidence checklist.
4. Finalize public statements/translations against those facts. Only then mark
   genuine approvals, set the actual published revision/date and release IDs.
5. Publish the website through the existing hosting under owner control, verify
   its exact HTTPS routes, and synchronize the bundled notice. Do not activate
   production providers merely because the policy URL returns 200.

The privacy probe currently passes 61 assertions. The pre-existing broad
monetization probe builds and passes initial economy/save/navigation groups,
then fails on historical level gen:qc_mt:8. The failure also reproduces with
the committed baseline project/settings and unchanged remaining dependencies.
No campaign data was changed; no full-regression pass is claimed.

A failed choice save cannot activate a new SDK opt-in. The UI surfaces save
errors; restart/backup behavior after failed writes remains a required device
case. Deferred transports avoid invoking SDK factories before eligible opt-in.
