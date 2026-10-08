# Quiet Camp sample

`quietcamp.levelprofile.json` — a format profile that maps Level Kit onto the
Quiet Camp level schema: flat top-level layer keys (`entry`, `blocked`,
`shade`, `noise`), per-kind entity arrays (`guests`, `witness`), and verbatim
top-level extras (`friends`, `lighting`, `tutorialKey`, `contentHash`, …).

`QC_SAMPLE.json` — a real Quiet Camp level file; parses cleanly with the
profile and round-trips losslessly.

Drop the profile into any project — the Level Designer picks it up
automatically (as "quiet-camp"), and authored files are directly loadable by
Quiet Camp's `LevelLoader`.
