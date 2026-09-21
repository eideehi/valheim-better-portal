// il-verify: a static verification harness for the IL shape that BetterPortal's
// Harmony transpiler for Game.ConnectPortals depends on.
//
// This tool does NOT execute the mod's transpiler (Harmony cannot run in this
// environment; see tools/il-verify/README.md). It only reads the *game's*
// assembly_valheim.dll with Mono.Cecil and asserts that the exact opcode
// sequences the transpiler pattern-matches are still present, in the shape
// the transpiler expects. If a Valheim update changes this method, this tool
// is meant to fail loudly here instead of the transpiler silently matching
// nothing (or the wrong thing) at runtime.

using Mono.Cecil;
using Mono.Cecil.Cil;

int exitCode = 0;

string assemblyPath = ResolveAssemblyPath(args);
if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"[FATAL] assembly not found: {assemblyPath}");
    return 2;
}

Console.WriteLine("== il-verify: BetterPortal / Game.ConnectPortals IL structure check ==");
Console.WriteLine($"Assembly: {assemblyPath}");

AssemblyDefinition asm;
try
{
    asm = AssemblyDefinition.ReadAssembly(assemblyPath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[FATAL] failed to read assembly with Mono.Cecil: {ex}");
    return 2;
}

PrintVersion(asm);

TypeDefinition? gameType = asm.MainModule.GetType("Game");
if (gameType is null)
{
    Console.Error.WriteLine("[FATAL] could not find type 'Game' in the assembly.");
    return 2;
}

MethodDefinition? connectPortals = gameType.Methods.FirstOrDefault(
    m => m.Name == "ConnectPortals" && !m.HasParameters);
if (connectPortals is null)
{
    Console.Error.WriteLine("[FATAL] could not find method 'Game::ConnectPortals()' (no-arg overload).");
    return 2;
}
if (!connectPortals.HasBody)
{
    Console.Error.WriteLine("[FATAL] 'Game::ConnectPortals()' has no method body.");
    return 2;
}

List<Instruction> ins = connectPortals.Body.Instructions.ToList();
Console.WriteLine($"Found Game::ConnectPortals() — {ins.Count} instructions, code size 0x{connectPortals.Body.CodeSize:x}.");
Console.WriteLine();

// ---------------------------------------------------------------------
// Check 1 — destination-tag read sites
// ---------------------------------------------------------------------
Console.WriteLine("--- Check 1: destination-tag read sites (ldsfld ZDOVars::s_tag / ldstr \"\" / callvirt ZDO::GetString / store-local) ---");
{
    var matches = new List<int>();
    var nearMisses = new List<int>();

    for (int i = 0; i + 3 < ins.Count; i++)
    {
        if (!IsLdsfld(ins[i], "ZDOVars", "s_tag")) continue;
        if (!IsLdstrEmpty(ins[i + 1])) continue;
        if (!IsCall(ins[i + 2], Code.Callvirt, "ZDO", "GetString", 2)) continue;

        if (IsStoreLocal(ins[i + 3]))
            matches.Add(i);
        else
            nearMisses.Add(i);
    }

    bool ok = matches.Count == 2;
    ReportResult(ok, "exactly 2 destination-tag read sites (ldsfld/ldstr/callvirt/store-local)", ref exitCode);

    foreach (int i in matches)
        Console.WriteLine($"    matched at {Off(ins[i])}..{Off(ins[i + 3])}: {DescribeRange(ins, i, 4)}");

    if (nearMisses.Count > 0)
    {
        Console.WriteLine($"    (info) {nearMisses.Count} additional ldsfld ZDOVars::s_tag/ldstr/callvirt site(s) NOT followed by a store-local — correctly excluded:");
        foreach (int i in nearMisses)
            Console.WriteLine($"      at {Off(ins[i])}: 4th instruction is {ins[i + 3].OpCode.Name} (expected a stloc*) — {DescribeRange(ins, i, 4)}");
    }

    if (!ok)
    {
        Console.WriteLine("    Context — all candidate 3-instruction prefixes found:");
        for (int i = 0; i + 2 < ins.Count; i++)
        {
            if (IsLdsfld(ins[i], "ZDOVars", "s_tag") && IsLdstrEmpty(ins[i + 1]) && IsCall(ins[i + 2], Code.Callvirt, "ZDO", "GetString", 2))
                Console.WriteLine($"      at {Off(ins[i])}: next instruction = {OpNameAt(ins, i + 3)}");
        }
    }
}
Console.WriteLine();

// ---------------------------------------------------------------------
// Check 2 — connection bookkeeping call (AddToCurrentlyConnectingPortals)
// ---------------------------------------------------------------------
Console.WriteLine("--- Check 2: connection bookkeeping call (ldarg.0 / ldloc* / ldloc* / call Game::AddToCurrentlyConnectingPortals(ZDO, ZDO)) ---");
int check2CallIndex = -1;
{
    var matches = new List<int>();
    for (int i = 0; i + 3 < ins.Count; i++)
    {
        if (!IsLdarg0(ins[i])) continue;
        if (!TryLocalIndex(ins[i + 1], out _)) continue;
        if (!TryLocalIndex(ins[i + 2], out _)) continue;
        if (!IsCall(ins[i + 3], Code.Call, "Game", "AddToCurrentlyConnectingPortals", 2)) continue;
        matches.Add(i);
    }

    bool ok = matches.Count == 1;
    ReportResult(ok, "exactly 1 AddToCurrentlyConnectingPortals bookkeeping call", ref exitCode);

    foreach (int i in matches)
        Console.WriteLine($"    matched at {Off(ins[i])}..{Off(ins[i + 3])}: {DescribeRange(ins, i, 4)}");

    if (matches.Count == 1)
        check2CallIndex = matches[0] + 3;

    if (!ok)
    {
        Console.WriteLine("    Context — all calls to Game::AddToCurrentlyConnectingPortals found in the method:");
        for (int i = 0; i < ins.Count; i++)
        {
            if (IsCall(ins[i], Code.Call, "Game", "AddToCurrentlyConnectingPortals", 2))
                Console.WriteLine($"      call at {Off(ins[i])}, preceding 3 instrs: {DescribeRange(ins, Math.Max(0, i - 3), 3)}");
        }
    }
}
Console.WriteLine();

// ---------------------------------------------------------------------
// Check 3 — forward and reverse SetConnection
// ---------------------------------------------------------------------
Console.WriteLine("--- Check 3: forward/reverse SetConnection (ldarg.0 / ldloc* / ldloc* / ldfld ZDO::m_uid / ldc.i4.0 / call Game::SetConnection(ZDO, ZDOID, bool)), after Check 2 ---");
{
    var matches = new List<(int start, int a, int b)>();
    int searchStart = check2CallIndex >= 0 ? check2CallIndex + 1 : 0;

    for (int i = searchStart; i + 5 < ins.Count; i++)
    {
        if (!IsLdarg0(ins[i])) continue;
        if (!TryLocalIndex(ins[i + 1], out int a)) continue;
        if (!TryLocalIndex(ins[i + 2], out int b)) continue;
        if (!IsLdfld(ins[i + 3], "ZDO", "m_uid")) continue;
        if (!IsLdcI4(ins[i + 4], 0)) continue;
        if (!IsCall(ins[i + 5], Code.Call, "Game", "SetConnection", 3)) continue;
        matches.Add((i, a, b));
    }

    bool countOk = matches.Count == 2;
    ReportResult(countOk, "exactly 2 SetConnection calls (forward + reverse) after the bookkeeping call", ref exitCode);

    foreach (var m in matches)
        Console.WriteLine($"    matched at {Off(ins[m.start])}..{Off(ins[m.start + 5])} (locals {m.a},{m.b}): {DescribeRange(ins, m.start, 6)}");

    // The transpiler does not take "the second match" as the reverse link: it reads the two
    // locals the bookkeeping call itself loads - AddToCurrentlyConnectingPortals(source, target) -
    // and classifies each following SetConnection by which of *those* locals it loads first.
    // (source, target) is the forward link, (target, source) is the reverse link it NOPs.
    // A pair that is merely each other's reverse but built from a different pair of locals would
    // leave the transpiler with zero forward and zero reverse matches, so anchor on the
    // bookkeeping call's locals here as well.
    if (check2CallIndex >= 2 &&
        TryLocalIndex(ins[check2CallIndex - 2], out int sourceLocal) &&
        TryLocalIndex(ins[check2CallIndex - 1], out int targetLocal))
    {
        Console.WriteLine($"    bookkeeping call at {Off(ins[check2CallIndex])} loads source local {sourceLocal}, target local {targetLocal}.");

        if (sourceLocal == targetLocal)
        {
            ReportResult(false,
                $"the bookkeeping call loads two distinct locals (both are local {sourceLocal}, so the transpiler cannot tell the forward link from the reverse one)",
                ref exitCode);
        }
        else
        {
            var forward = matches.Where(m => m.a == sourceLocal && m.b == targetLocal).ToList();
            var reverse = matches.Where(m => m.a == targetLocal && m.b == sourceLocal).ToList();

            ReportResult(forward.Count == 1 && reverse.Count == 1,
                $"exactly 1 forward SetConnection(local {sourceLocal}, local {targetLocal}) and exactly 1 reverse SetConnection(local {targetLocal}, local {sourceLocal}), anchored on the bookkeeping call's own locals",
                ref exitCode);

            foreach (var m in forward)
                Console.WriteLine($"      forward link at {Off(ins[m.start + 5])} (locals {m.a},{m.b})");
            foreach (var m in reverse)
                Console.WriteLine($"      reverse link at {Off(ins[m.start + 5])} (locals {m.a},{m.b}) — this is the call BetterPortal NOPs");

            var unclassified = matches.Where(m => !forward.Contains(m) && !reverse.Contains(m)).ToList();
            foreach (var m in unclassified)
                Console.WriteLine($"      NOT classified: match at {Off(ins[m.start + 5])} loads locals ({m.a},{m.b}), which is neither ({sourceLocal},{targetLocal}) nor ({targetLocal},{sourceLocal}) — the transpiler would ignore it");

            if (forward.Count != 1 || reverse.Count != 1)
            {
                Console.WriteLine($"    Context: found {forward.Count} forward and {reverse.Count} reverse match(es); the transpiler requires exactly 1 of each.");
            }
        }
    }
    else
    {
        Console.WriteLine("    (info) forward/reverse classification skipped: Check 2 did not identify a unique bookkeeping call to read the source/target locals from.");
    }

    if (!countOk)
    {
        Console.WriteLine("    Context — all candidate matches (before requiring exactly 2), including those before Check 2's call:");
        for (int i = 0; i + 5 < ins.Count; i++)
        {
            if (!IsLdarg0(ins[i])) continue;
            if (!TryLocalIndex(ins[i + 1], out int a)) continue;
            if (!TryLocalIndex(ins[i + 2], out int b)) continue;
            if (!IsLdfld(ins[i + 3], "ZDO", "m_uid")) continue;
            if (!IsLdcI4(ins[i + 4], 0)) continue;
            if (!IsCall(ins[i + 5], Code.Call, "Game", "SetConnection", 3)) continue;
            Console.WriteLine($"      at {Off(ins[i])} (locals {a},{b}) [{(i >= searchStart ? "after Check2" : "BEFORE Check2 — excluded")}]");
        }
    }
}
Console.WriteLine();

// ---------------------------------------------------------------------
// Check 4 — Valheim 1.0 stale-connection clause
// ---------------------------------------------------------------------
Console.WriteLine("--- Check 4: stale-connection clause (ldloc* / ldc.i4.1 / callvirt ZDO::GetConnectionZDOID / ldsfld ZDOID::None / call ZDOID::op_Equality / brfalse[.s]) ---");
{
    var matches = new List<(int start, Instruction branch)>();
    for (int i = 0; i + 5 < ins.Count; i++)
    {
        if (!TryLocalIndex(ins[i], out _)) continue;
        if (!IsLdcI4(ins[i + 1], 1)) continue;
        if (!IsCall(ins[i + 2], Code.Callvirt, "ZDO", "GetConnectionZDOID", 1)) continue;
        if (!IsLdsfld(ins[i + 3], "ZDOID", "None")) continue;
        if (!IsCall(ins[i + 4], Code.Call, "ZDOID", "op_Equality", 2)) continue;
        if (!IsBrfalse(ins[i + 5])) continue;
        matches.Add((i, ins[i + 5]));
    }

    bool countOk = matches.Count == 1;
    ReportResult(countOk, "exactly 1 stale-connection clause", ref exitCode);

    foreach (var m in matches)
        Console.WriteLine($"    matched at {Off(ins[m.start])}..{Off(ins[m.start + 5])}: {DescribeRange(ins, m.start, 6)}");

    if (!countOk)
    {
        Console.WriteLine("    Context — all ldloc*/ldc.i4.1/callvirt GetConnectionZDOID sequences found:");
        for (int i = 0; i + 2 < ins.Count; i++)
        {
            if (!TryLocalIndex(ins[i], out _)) continue;
            if (!IsLdcI4(ins[i + 1], 1)) continue;
            if (!IsCall(ins[i + 2], Code.Callvirt, "ZDO", "GetConnectionZDOID", 1)) continue;
            Console.WriteLine($"      at {Off(ins[i])}: next instr = {OpNameAt(ins, i + 3)}");
        }
    }
    else
    {
        var (start, branch) = matches[0];
        var target = (Instruction)branch.Operand;

        // Find "the disconnect at IL_0088-IL_0090": the call to
        // Game::SetConnection whose ZDOID argument is ldsfld ZDOID::None.
        var disconnectCalls = new List<int>();
        for (int i = 0; i + 2 < ins.Count; i++)
        {
            if (!IsLdsfld(ins[i], "ZDOID", "None")) continue;
            if (!IsLdcI4(ins[i + 1], 0)) continue;
            if (!IsCall(ins[i + 2], Code.Call, "Game", "SetConnection", 3)) continue;
            disconnectCalls.Add(i + 2);
        }

        if (disconnectCalls.Count != 1)
        {
            ReportResult(false,
                "exactly 1 disconnect call (Game::SetConnection with ZDOID::None argument) to anchor the branch-target assumption", ref exitCode);
            foreach (int i in disconnectCalls)
                Console.WriteLine($"    found disconnect-shaped call at {Off(ins[i])}");
        }
        else
        {
            int disconnectCallIndex = disconnectCalls[0];
            int targetIndex = ins.IndexOf(target);
            bool afterDisconnect = targetIndex > disconnectCallIndex;
            ReportResult(afterDisconnect,
                $"the brfalse branch target ({Off(target)}) is positioned after the disconnect call ({Off(ins[disconnectCallIndex])}), so turning the brfalse into an unconditional br skips the disconnect block",
                ref exitCode);
            if (!afterDisconnect)
            {
                Console.WriteLine($"    Context: branch target index {targetIndex} ({Off(target)}), disconnect call index {disconnectCallIndex} ({Off(ins[disconnectCallIndex])}).");
            }
            else
            {
                Console.WriteLine($"    branch target instruction: {Describe(target)}");
                // Informational only. The assertion above verifies the ordering, which is what the
                // rewrite needs; it does not by itself prove the target *is* the loop advance.
                Console.WriteLine(IsLoopAdvance(ins, targetIndex)
                    ? $"    (info) the target also begins the enumerator loop-advance block: {DescribeRange(ins, targetIndex, 3)}"
                    : "    (info) the target was not recognised as an enumerator loop-advance block (load / call MoveNext / backward conditional branch); the assertion above only covers the ordering, so review the surrounding IL by hand.");
            }
        }
    }
}
Console.WriteLine();

Console.WriteLine(exitCode == 0
    ? "== RESULT: ALL CHECKS PASSED =="
    : "== RESULT: ONE OR MORE CHECKS FAILED ==");

return exitCode;

// =======================================================================
// Helpers
// =======================================================================

static string ResolveAssemblyPath(string[] args)
{
    if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        return args[0];

    string? valheimDir = Environment.GetEnvironmentVariable("VALHEIM_DIR");
    if (!string.IsNullOrWhiteSpace(valheimDir))
        return Path.Combine(valheimDir, "valheim_Data", "Managed", "assembly_valheim.dll");

    Console.Error.WriteLine("Usage: il-verify <path-to-assembly_valheim.dll>");
    Console.Error.WriteLine("   or: VALHEIM_DIR=<Valheim root> il-verify");
    Environment.Exit(2);
    return string.Empty;
}

static void PrintVersion(AssemblyDefinition asm)
{
    try
    {
        TypeDefinition? versionType = asm.MainModule.GetType("Version");
        MethodDefinition? cctor = versionType?.Methods.FirstOrDefault(m => m.Name == ".cctor");
        if (versionType is not null && cctor is not null && cctor.HasBody)
        {
            var body = cctor.Body.Instructions;
            for (int i = 0; i + 3 < body.Count; i++)
            {
                if (body[i + 3].OpCode.Code == Code.Newobj &&
                    body[i + 3].Operand is MethodReference ctorRef &&
                    ctorRef.DeclaringType.Name == "GameVersion" &&
                    ctorRef.Name == ".ctor" &&
                    // The three-constant window only lines up with (major, minor, patch) if the
                    // constructor really takes exactly three arguments. If GameVersion ever gains
                    // a parameter, fall through to the fallback instead of printing a wrong number.
                    ctorRef.Parameters.Count == 3 &&
                    TryLdcI4(body[i], out int major) &&
                    TryLdcI4(body[i + 1], out int minor) &&
                    TryLdcI4(body[i + 2], out int patch))
                {
                    Console.WriteLine($"Game version (from Version::.cctor / GameVersion({major},{minor},{patch})): {major}.{minor}.{patch}");
                    return;
                }
            }
        }
    }
    catch
    {
        // fall through to fallback below
    }

    Console.WriteLine("Game version: could not be read from Version::.cctor (structure not as expected); falling back to assembly identity.");
    Console.WriteLine($"  Assembly MVID: {asm.MainModule.Mvid}");
    Console.WriteLine($"  Assembly file: {asm.MainModule.FileName}");
}

static bool TryLdcI4(Instruction i, out int value)
{
    switch (i.OpCode.Code)
    {
        case Code.Ldc_I4_0: value = 0; return true;
        case Code.Ldc_I4_1: value = 1; return true;
        case Code.Ldc_I4_2: value = 2; return true;
        case Code.Ldc_I4_3: value = 3; return true;
        case Code.Ldc_I4_4: value = 4; return true;
        case Code.Ldc_I4_5: value = 5; return true;
        case Code.Ldc_I4_6: value = 6; return true;
        case Code.Ldc_I4_7: value = 7; return true;
        case Code.Ldc_I4_8: value = 8; return true;
        case Code.Ldc_I4_M1: value = -1; return true;
        case Code.Ldc_I4_S: value = (sbyte)i.Operand; return true;
        case Code.Ldc_I4: value = (int)i.Operand; return true;
        default: value = 0; return false;
    }
}

static bool IsLdsfld(Instruction i, string typeName, string fieldName)
{
    if (i.OpCode.Code != Code.Ldsfld) return false;
    return i.Operand is FieldReference f && f.DeclaringType.Name == typeName && f.Name == fieldName;
}

static bool IsLdfld(Instruction i, string typeName, string fieldName)
{
    if (i.OpCode.Code != Code.Ldfld) return false;
    return i.Operand is FieldReference f && f.DeclaringType.Name == typeName && f.Name == fieldName;
}

static bool IsLdstrEmpty(Instruction i)
    => i.OpCode.Code == Code.Ldstr && i.Operand is string s && s.Length == 0;

static bool IsCall(Instruction i, Code exactOpcode, string typeName, string methodName, int paramCount)
{
    if (i.OpCode.Code != exactOpcode) return false;
    if (i.Operand is not MethodReference m) return false;
    return m.DeclaringType.Name == typeName && m.Name == methodName && m.Parameters.Count == paramCount;
}

static bool IsLdarg0(Instruction i) => i.OpCode.Code == Code.Ldarg_0;

static bool IsLdcI4(Instruction i, int value) => TryLdcI4(i, out int v) && v == value;

static bool IsStoreLocal(Instruction i)
{
    switch (i.OpCode.Code)
    {
        case Code.Stloc_0:
        case Code.Stloc_1:
        case Code.Stloc_2:
        case Code.Stloc_3:
        case Code.Stloc_S:
        case Code.Stloc:
            return true;
        default:
            return false;
    }
}

static bool TryLocalIndex(Instruction i, out int index)
{
    switch (i.OpCode.Code)
    {
        case Code.Ldloc_0: index = 0; return true;
        case Code.Ldloc_1: index = 1; return true;
        case Code.Ldloc_2: index = 2; return true;
        case Code.Ldloc_3: index = 3; return true;
        case Code.Ldloc_S:
        case Code.Ldloc:
            index = ((VariableDefinition)i.Operand).Index;
            return true;
        default:
            index = -1;
            return false;
    }
}

static bool IsBrfalse(Instruction i) => i.OpCode.Code is Code.Brfalse or Code.Brfalse_S;

// Recognises the C# foreach loop-advance block: a load of the enumerator, a MoveNext call and a
// conditional branch back to an earlier instruction. Used for reporting only, never for PASS/FAIL,
// so that a future compiler emitting a different loop shape does not fail the run on its own.
static bool IsLoopAdvance(List<Instruction> ins, int index)
{
    if (index < 0 || index + 2 >= ins.Count) return false;

    bool loadsEnumerator = ins[index].OpCode.Code
        is Code.Ldloca or Code.Ldloca_S
        or Code.Ldloc or Code.Ldloc_S or Code.Ldloc_0 or Code.Ldloc_1 or Code.Ldloc_2 or Code.Ldloc_3;
    if (!loadsEnumerator) return false;

    if (ins[index + 1].OpCode.Code is not (Code.Call or Code.Callvirt)) return false;
    if (ins[index + 1].Operand is not MethodReference moveNext || moveNext.Name != "MoveNext") return false;

    if (ins[index + 2].OpCode.Code is not (Code.Brtrue or Code.Brtrue_S)) return false;
    return ins[index + 2].Operand is Instruction back && back.Offset < ins[index].Offset;
}

static string Off(Instruction i) => $"IL_{i.Offset:x4}";

static string Describe(Instruction i)
{
    string operand = i.Operand switch
    {
        FieldReference f => $"{f.DeclaringType.Name}::{f.Name}",
        MethodReference m => $"{m.DeclaringType.Name}::{m.Name}({string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name))})",
        Instruction target => Off(target),
        VariableDefinition v => $"[{v.Index}]",
        string s => $"\"{s}\"",
        null => string.Empty,
        var other => other.ToString() ?? string.Empty,
    };
    return $"{Off(i)} {i.OpCode.Name} {operand}".TrimEnd();
}

// Diagnostic helpers below are deliberately bounds-tolerant: they are used on the
// failure paths, where a matched prefix may sit at the very end of the method body.
static string OpNameAt(List<Instruction> ins, int index)
    => index >= 0 && index < ins.Count ? ins[index].OpCode.Name : "<end of method body>";

static string DescribeRange(List<Instruction> ins, int start, int count)
{
    int from = Math.Max(0, start);
    int end = Math.Min(ins.Count, start + count);
    if (end <= from) return "<no instructions>";

    string text = string.Join("  |  ", Enumerable.Range(from, end - from).Select(k => Describe(ins[k])));
    return end - from < count ? text + "  |  <end of method body>" : text;
}

static void ReportResult(bool ok, string assertion, ref int exitCode)
{
    Console.WriteLine((ok ? "  [PASS] " : "  [FAIL] ") + assertion);
    if (!ok) exitCode = 1;
}
