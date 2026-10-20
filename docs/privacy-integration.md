# Quiet Camp privacy website integration — 2026-10-20

Canonical target: https://oleksiy-havryliuk.de/quiet-camp/privacy

The real custom-domain website is the portfolio Vite project in
`kruty1918dev-ai/portfolio-oleksii-ha`, strongly linked by matching live HTML at
the official domain and portfolio-oleksii-ha.vercel.app and a successful Vercel
commit status. Its account binding still needs owner confirmation. The policy
is prepared there as complete EN/UK/DE static HTML under
`public/quiet-camp/privacy/`, with exact Vercel route rewrites. The separate
GitHub Pages guide in this repository is not being assigned the custom domain.

The guide footer now links to the official policy and has uk/en/de dictionary
labels. The required URL currently returns 404 over HTTPS: no deployment has
been authorized. Release the footer and Unity config only after the website is
published and checked. There is no local duplicate legal policy on Pages.

Unity already renders a localized Privacy Policy action under Settings →
Privacy → Documents in PrivacyPanel.cs. It validates HTTPS and calls
Application.OpenURL. legal.json now supplies the canonical URL and technical
support email; no scene hookup or C# gameplay change is required for this link.
It is accessible without an optional Analytics consent. The analytics notice
also uses the same central URL when the provider is available. Do not ship the
intermediate link before its destination is live.

The config edit does not populate publisher/revision/publishedOn, does not
approve teen privacy or SDK disclosures, and does not enable Analytics or
production ads. privacy_content.json remains a bundled draft with no remote
origin. The boot document will need an approved revision, complete translated
body, correct origins/variants/hashes and summaries before its later legal
publication. Reading that document remains separate from tracking consent.

Audited game main: 01784d3614c643eac487baa25d589dce6ed2c0ef. Remote heads were
rechecked: only main exists in this game repository at that commit. No Firebase
SDK or Crashlytics installation was present in the attached checkout; a reported
newer developer-local installation was not available. This does not establish
that Firebase is absent from the developer’s actual machine or intended release.

See the portfolio delivery's reports/quiet-camp/ for source anchors, data
inventory (Markdown/CSV), Data Safety mapping, legal/release blockers, hosting
instructions, browser tests and screenshots. No Unity Editor/player build,
public push, deployment, DNS change or email send was performed.
