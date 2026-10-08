# AgentVerify command samples

Copy one of these files to `<project>/agentverify.commands.json`,
then run:

```bash
Unity.exe -batchmode -projectPath . \
  -executeMethod Kruty1918.AgentVerify.EditorTools.AgentBatch.RunPlayMode
```

- `menu_flow.json` — summary → menu screenshot/dump → click Play →
  wait → game screenshot/dump → error check → log dump
- `visual_check.json` — single 1280×720 frame + camera/error checks

Adjust `"target"` names (`PlayButton`, `MainCamera`) to your project —
the `interactables` list in a `describe`/`summary` result tells you
what the agent can actually click.
