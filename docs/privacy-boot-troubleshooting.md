# Privacy publication startup check

`BootPrivacyPanel.Create` rejects published policy variants whose revision differs
from `Resources/QuietCamp/legal.json`, or whose HTTPS URL is outside the policy
document's allowed origins. Keep this validation enabled.

On 2026-10-24, local legal metadata had regressed to
`2026-10-24.public-policy-10-ads-privacy-choices` while the bundled uk/en/de policy
and summaries used `2026-10-24.public-policy-11-shared-player-accounts`.
Restoring the metadata revision and aligning `publishedPolicyRevision` in the
owner configuration resolves that mismatch without changing policy text or
service approval flags. The ignored `Config/owner.local.json` must also agree
before using the owner preparation workflow.

Run the resource consistency and synchronization contracts without Unity:

```sh
python -m unittest discover -s tools -p test_sync_privacy_policy.py
python -m unittest discover -s tools -p test_owner_config.py
```

The first command checks actual bundled metadata, all three variants, HTTPS
origins and summary revisions, as well as temporary draft/publication fixtures.
The second checks owner configuration generation and rejects mismatched
published revisions. These checks do not launch Unity or prove startup UI flow.
