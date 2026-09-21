# il-verify

`BetterPortal` rewrites `Game.ConnectPortals` with a Harmony **IL transpiler**. That transpiler works by pattern-matching fixed opcode sequences in the game's compiled IL and rewriting the instructions it finds. If a Valheim build shifts the IL of that method even slightly, the transpiler can silently match nothing — or match the wrong place.

`il-verify` is a standalone tool that reads `Game::ConnectPortals()` out of the current `assembly_valheim.dll` with Mono.Cecil and mechanically asserts that the IL structure the transpiler depends on **is still there**.

## What this does not verify (important)

The tool **never executes BetterPortal's transpiler**. That is an environment constraint:

- Harmony's (`0Harmony.dll`) static initialization depends on MonoMod and does not work correctly on the .NET 8 runtime (verified in practice).
- `assembly_valheim.dll` targets .NET Framework 4.8, so running it needs Mono, which is not installed in this environment.

So `il-verify` does not answer "does the transpiler work?" — it answers only **"is the game-side IL the transpiler assumes still shaped that way?"**, by loading the assembly statically with Mono.Cecil. It is no proof that the rewriting logic itself (operand substitution, NOPing, branch rewriting) behaves correctly.

It also does **not** verify the stack premise the rewrite relies on: that the stale-connection clause (check 4) begins with an **empty evaluation stack**. That is what makes it safe to replace five instructions with `Nop` and turn the conditional branch into an unconditional one while keeping the method stack-balanced. The tool checks the opcode shape and the branch target, but not the stack state, and this is the assumption most likely to be violated silently if a future Valheim build folds the clause into a larger expression. If check 4 still passes but the clause now sits inside a bigger expression, that has to be reviewed by hand.

## The four premises it checks

The tool searches `Game::ConnectPortals()` for the following four IL patterns and verifies that each occurs exactly as often as expected (offsets in parentheses are as of Valheim 1.0.15).

1. **Destination-tag read sites (exactly 2)**
   The four-instruction sequence `ldsfld ZDOVars::s_tag` → `ldstr ""` → `callvirt ZDO::GetString(int32, string)` → store to a local (`stloc*`).
   The mod redirects the `ldsfld` operand of these two sites to its own destination-tag field.
   The method contains a third `s_tag` read (`IL_005c` in 1.0.15), but it is followed by `ldloc.s` (a load, not a store) and is therefore out of scope. The tool asserts "exactly 2"; "2 or more" does not pass.

2. **Connection bookkeeping call (exactly 1)**
   `ldarg.0` → local load → local load → `call Game::AddToCurrentlyConnectingPortals(ZDO, ZDO)`.
   The mod NOPs these four instructions.

3. **Forward and reverse `SetConnection` (exactly 2 after check 2)**
   `ldarg.0` → local load → local load → `ldfld ZDO::m_uid` → `ldc.i4.0` → `call Game::SetConnection(ZDO, ZDOID, bool)`.
   One is the forward link and one is the reverse link; the mod NOPs the reverse one to make portals one-directional.
   The transpiler does not treat "the second match" as the reverse link. It reads the two locals the bookkeeping call itself loads — `AddToCurrentlyConnectingPortals(source, target)` — and classifies each following `SetConnection` by which of *those* locals it loads first: `(source, target)` is forward, `(target, source)` is reverse. The tool mirrors that: it asserts exactly one forward and exactly one reverse match **anchored on the bookkeeping call's own locals**, and prints the local indices either way. A pair of calls that are merely each other's reverse but built from a different pair of locals fails here, because the transpiler would find zero forward and zero reverse matches and bail out.

4. **The "stale connection" clause added in Valheim 1.0 (exactly 1)**
   `ldloc*` → `ldc.i4.1` → `callvirt ZDO::GetConnectionZDOID(ConnectionType)` → `ldsfld ZDOID::None` → `call ZDOID::op_Equality(ZDOID, ZDOID)` → `brfalse`/`brfalse.s`.
   This block is new in Valheim 1.0 and conflicts with BetterPortal's one-directional portals, so the mod disables it by NOPing the first five instructions and rewriting the branch into an unconditional `br`.
   For that rewrite to be safe, the `brfalse` target must lie **after** the disconnect block (the `SetConnection` call that takes `ZDOID::None`, `IL_0088`–`IL_0090` in 1.0.15), so that the unconditional branch skips the disconnect. The tool asserts exactly that ordering — nothing more. It additionally reports, as information only, whether the target happens to begin the enumerator loop-advance block (a load, a `MoveNext` call and a backward conditional branch); that line never decides PASS/FAIL, so a future build with a different loop shape is not failed on its own.

Every check prints PASS or FAIL, and on FAIL it prints the opcodes and offsets actually found. PASS also prints the matched offsets, so a run doubles as a record of the game-side layout at that point in time. If any single check fails, the process exits with a non-zero exit code.

The first lines also print, for reference, the game version read from the `GameVersion(major, minor, patch)` construction in `Version::.cctor`. The constructor reference must take exactly three parameters; otherwise the three-constant window cannot be trusted and the tool falls back to reporting the assembly's MVID and file path instead of printing a possibly wrong number. Failing to read the version line is never itself a verification failure.

## How to run it

From the repository root (`VALHEIM_DIR` is the same environment variable as the other build steps in `AGENTS.md`):

```bash
VALHEIM_DIR=/path/to/Valheim dotnet run --project tools/il-verify -c Release
```

The assembly path can also be passed directly as an argument, in which case `VALHEIM_DIR` is not needed:

```bash
dotnet run --project tools/il-verify -c Release -- /path/to/Valheim/valheim_Data/Managed/assembly_valheim.dll
```

Exit code `0` means every check passed; anything else means at least one failed. Scripts and CI should branch on the exit code.

## Getting Mono.Cecil

The tool references the NuGet `Mono.Cecil` package (0.11.6) as a `PackageReference`. Where NuGet restore is unavailable (offline, for example), switch to referencing `Mono.Cecil.dll` from the Valheim installation's `BepInEx/core` (or `core-bak`) directly, via `<Reference Include="Mono.Cecil"><HintPath>...</HintPath></Reference>`.

## What to do when it fails

A failing check means the IL structure of `Game.ConnectPortals` changed. Compare the opcodes and offsets in the output against the transpiler implementation (`BetterPortal/Patches.cs`) and update the match conditions or the rewrite positions there. Do not loosen a check just to make it pass — that defeats the purpose of the tool, which is to detect a collapsed premise early and loudly.
