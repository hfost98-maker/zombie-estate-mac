using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class PatchAiPlayer2 {
    const int PlayerIndexOne = 1;

    static int Main(string[] args) {
        string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        string gameExe = Path.Combine(root, "game", "ZombieEstate.exe");
        string aiSource = Path.Combine(root, "scripts", "AiTeammate.cs");
        string modsSource = Path.Combine(root, "scripts", "GameMods.cs");
        string dualSource = Path.Combine(root, "scripts", "DualScreen.cs");
        string aiDll = Path.Combine(root, "tools", "AiTeammate.dll");
        string fna = Path.Combine(root, "lib", "FNA.dll");

        if (!File.Exists(gameExe)) {
            Console.Error.WriteLine("Missing " + gameExe);
            return 1;
        }

        string backup = gameExe + ".pre-ai-p2-patch";
        if (!File.Exists(backup))
            File.Copy(gameExe, backup, false);
        if (!File.Exists(aiSource)) {
            Console.Error.WriteLine("Missing " + aiSource);
            return 1;
        }

        if (!File.Exists(modsSource)) {
            Console.Error.WriteLine("Missing " + modsSource);
            return 1;
        }

        if (!File.Exists(dualSource)) {
            Console.Error.WriteLine("Missing " + dualSource);
            return 1;
        }

        if (!CompileAiDll(aiSource, modsSource, dualSource, aiDll, gameExe, fna))
            return 1;

        var asm = AssemblyDefinition.ReadAssembly(gameExe, new ReaderParameters {
            ReadWrite = true,
            InMemory = true
        });
        var module = asm.MainModule;
        var inputType = module.Types.First(t => t.FullName == "ZombieEstate.InputManager");

        if (module.AssemblyReferences.Any(r => r.Name == "AiTeammate")) {
            Console.WriteLine("Removing broken AiTeammate assembly reference...");
            foreach (var typeName in new[] { "AiTeammate", "AiPlayerState", "GameMods", "DualScreen" }) {
                var broken = module.Types.FirstOrDefault(t => t.Name == typeName);
                if (broken != null)
                    module.Types.Remove(broken);
            }
            var aiRef = module.AssemblyReferences.First(r => r.Name == "AiTeammate");
            module.AssemblyReferences.Remove(aiRef);
        }

        if (inputType.Methods.Any(m => m.Name == "UpdateInputs" && MethodAlreadyAiPatched(m))
            && inputType.Methods.Any(m => m.Name == "Held" && MethodAlreadyKeyboardBlocked(m))
            && inputType.Methods.Any(m => m.Name == "LeftStickForward" && MethodAlreadyGamepadOnlyRouted(m))) {
            var existingWorld = module.Types.First(t => t.FullName == "ZombieEstate.GameWorld");
            var existingGame1 = module.Types.First(t => t.FullName == "ZombieEstate.Game1");
            var existingAi = module.Types.First(t => t.FullName == "ZombieEstate.AiTeammate");
            PatchTryUpdateCameraP1Only(existingWorld, existingAi);
            PatchGame1DualScreenEndDraw(existingGame1);
            PatchGame1DualScreenHudFilter(existingGame1);
            PatchGame1DualScreenShopFilter(existingGame1);
            PatchNetStubCoopMouse(root);
            PatchFnaCoopKeyboardMerge(root);
            asm.Write(gameExe);
            Console.WriteLine("AI player 2 patch already applied.");
            return 0;
        }

        InjectType(module, aiDll);
        var aiType = module.Types.First(t => t.FullName == "ZombieEstate.AiTeammate");
        var modsType = module.Types.First(t => t.FullName == "ZombieEstate.GameMods");
        var charSelectType = module.Types.First(t => t.FullName == "ZombieEstate.CharacterSelection");
        var game1Type = module.Types.First(t => t.FullName == "ZombieEstate.Game1");

        PatchUpdateInputs(inputType, aiType);
        PatchBoolPlayerIndex(inputType, aiType, "LeftStickForward", "QueryLeftStickForward", "GamepadLeftStickForward", false);
        PatchBoolPlayerIndex(inputType, aiType, "LeftStickBack", "QueryLeftStickBack", "GamepadLeftStickBack", false);
        PatchBoolPlayerIndex(inputType, aiType, "LeftStickLeft", "QueryLeftStickLeft", "GamepadLeftStickLeft", false);
        PatchBoolPlayerIndex(inputType, aiType, "LeftStickRight", "QueryLeftStickRight", "GamepadLeftStickRight", false);
        PatchBoolPlayerIndex(inputType, aiType, "FirePressed", "QueryFirePressed", "GamepadFirePressed", true);
        PatchBoolPlayerIndex(inputType, aiType, "FireHeld", "QueryFireHeld", "GamepadFireHeld", false);
        PatchBoolPlayerIndex(inputType, aiType, "Aiming", "QueryAiming", "GamepadAiming", false);
        PatchBoolPlayerIndex(inputType, aiType, "StartPressed", "QueryStartPressed", "GamepadStartPressed", true);
        PatchBoolPlayerIndex(inputType, aiType, "APressed", "QueryAPressed", "GamepadAPressed", true);
        PatchBoolPlayerIndex(inputType, aiType, "BPressed", "QueryBPressed", "GamepadBPressed", true);
        PatchBoolPlayerIndex(inputType, aiType, "ChangeWepPressed", "QueryChangeWepPressed", "GamepadChangeWepPressed", true);
        PatchBoolPlayerIndex(inputType, aiType, "SpawnWavePressed", "QuerySpawnWavePressed", "GamepadSpawnWavePressed", true);
        PatchBoolPlayerIndex(inputType, aiType, "DPadUpPressed", "QueryDPadUp", "GamepadDPadUp", true);
        PatchBoolPlayerIndex(inputType, aiType, "DPadDownPressed", "QueryDPadDown", "GamepadDPadDown", true);
        PatchBoolPlayerIndex(inputType, aiType, "DPadLeftPressed", "QueryDPadLeft", "GamepadDPadLeft", true);
        PatchBoolPlayerIndex(inputType, aiType, "DPadRightPressed", "QueryDPadRight", "GamepadDPadRight", true);
        PatchBoolPlayerIndex(inputType, aiType, "ReloadHeld", "QueryReloadHeld", "GamepadReloadHeld", false);
        PatchBoolPlayerIndex(inputType, aiType, "ReloadPressed", "QueryReloadPressed", "GamepadReloadPressed", true);
        PatchAimAngle(inputType, aiType);
        PatchBlockNativeKeyboardForP2(inputType, aiType);
        PatchBlockCompatInputForP1(inputType, aiType);
        PatchSinglePlayerCoop(charSelectType);

        var gameWorldType = module.Types.First(t => t.FullName == "ZombieEstate.GameWorld");
        var playerType = module.Types.First(t => t.FullName == "ZombieEstate.Player");
        PatchTryUpdateCameraP1Only(gameWorldType, aiType);
        PatchGame1DualScreenEndDraw(game1Type);
        PatchGame1DualScreenHudFilter(game1Type);
        PatchGame1DualScreenShopFilter(game1Type);
        PatchMaxDistanceForAi(gameWorldType, aiType);
        PatchSkipCameraBounds(playerType, aiType);
        PatchGameWorldMapFiles(gameWorldType, modsType);
        PatchGame1LoadContent(game1Type, modsType);
        PatchCharacterSelectionMapSelect(charSelectType, modsType);
        PatchCharacterSelectionApplyMap(charSelectType, modsType);
        PatchGame1CarnivalClowns(game1Type, modsType);

        asm.Write(gameExe);
        PatchNetStubCoopMouse(root);
        PatchFnaCoopKeyboardMerge(root);
        Console.WriteLine("AI player 2 patch applied.");
        return 0;
    }

    static void PatchFnaCoopKeyboardMerge(string root) {
        foreach (var rel in new[] { "game/FNA.dll", "lib/FNA.dll" }) {
            string path = Path.Combine(root, rel);
            if (!File.Exists(path))
                continue;
            string backup = path + ".pre-coop-input-patch";
            if (!File.Exists(backup))
                File.Copy(path, backup, false);
            PatchFnaCoopKeyboardMergeFile(path);
        }
    }

    static void PatchFnaCoopKeyboardMergeFile(string path) {
        var asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters {
            ReadWrite = false,
            InMemory = true
        });
        var module = asm.MainModule;
        var gamePad = module.Types.First(t => t.FullName == "Microsoft.Xna.Framework.Input.GamePad");
        foreach (var method in gamePad.Methods.Where(m =>
                     m.Name == "GetState" && m.HasParameters && !m.HasGenericParameters)) {
            if (method.Body.Instructions.Any(i =>
                    i.OpCode == OpCodes.Ldstr && i.Operand as string == "ZOMBIE_ESTATE_KEYBOARD_P2")
                && method.Body.Instructions.Any(i =>
                    i.OpCode == OpCodes.Ldstr && i.Operand as string == "0"))
                continue;
            PrependSkipKeyboardMergeForP1WhenCoop(method, module);
            Console.WriteLine("Patched FNA GamePad." + method.Name + " coop keyboard block in " + path);
        }
        WriteAssemblySafely(asm, path);
    }

    static void PrependSkipKeyboardMergeForP1WhenCoop(MethodDefinition method, ModuleDefinition module) {
        var il = method.Body.GetILProcessor();
        var mergeCheck = method.Body.Instructions.First(i =>
            i.OpCode == OpCodes.Ldsfld
            && i.Operand is FieldReference fr
            && fr.Name == "XbligKeyboardGamePadEnabled");
        var cont = il.Create(OpCodes.Nop);
        var envGet = module.ImportReference(
            typeof(Environment).GetMethod("GetEnvironmentVariable", new[] { typeof(string) }));

        // Player 1 only: skip keyboard→gamepad merge unless solo keyboard mode (KEYBOARD_P2=0).
        il.InsertBefore(mergeCheck, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(mergeCheck, il.Create(OpCodes.Brtrue_S, cont));
        il.InsertBefore(mergeCheck, il.Create(OpCodes.Ldstr, "ZOMBIE_ESTATE_KEYBOARD_P2"));
        il.InsertBefore(mergeCheck, il.Create(OpCodes.Call, envGet));
        il.InsertBefore(mergeCheck, il.Create(OpCodes.Ldstr, "0"));
        il.InsertBefore(mergeCheck, il.Create(OpCodes.Call,
            module.ImportReference(typeof(string).GetMethod("op_Equality", new[] { typeof(string), typeof(string) }))));
        il.InsertBefore(mergeCheck, il.Create(OpCodes.Brtrue_S, cont));
        il.InsertBefore(mergeCheck, il.Create(OpCodes.Ldloc_0));
        il.InsertBefore(mergeCheck, il.Create(OpCodes.Ret));
        il.InsertBefore(mergeCheck, cont);
    }

    static void PatchNetStubCoopMouse(string root) {
        foreach (var rel in new[] { "game/FNA.NetStub.dll", "lib/FNA.NetStub.dll" }) {
            string path = Path.Combine(root, rel);
            if (!File.Exists(path))
                continue;
            PatchNetStubCoopMouseFile(path);
            PatchNetStubCoopAimFile(path);
        }
    }

    static void PatchNetStubCoopAimFile(string path) {
        var asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters {
            ReadWrite = false,
            InMemory = true
        });
        var module = asm.MainModule;
        var compatType = module.Types.First(t => t.Name == "ZombieEstateInputCompat");
        foreach (var name in new[] { "IsAiming", "GetAimAngle" }) {
            var method = compatType.Methods.First(m => m.Name == name && !m.HasGenericParameters);
            if (method.Body.Instructions.Any(i =>
                    i.OpCode == OpCodes.Ldstr && i.Operand as string == "ZOMBIE_ESTATE_KEYBOARD_P2")
                && method.Body.Instructions.Any(i =>
                    i.OpCode == OpCodes.Ldstr && i.Operand as string == "0"))
                continue;
            PrependBlockP1MouseWhenCoop(method, module, name == "GetAimAngle");
            Console.WriteLine("Patched FNA.NetStub " + name + " coop block in " + path);
        }
        WriteAssemblySafely(asm, path);
    }

    static void PatchNetStubCoopMouseFile(string path) {
        var asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters {
            ReadWrite = false,
            InMemory = true
        });
        var module = asm.MainModule;
        var compatType = module.Types.First(t => t.Name == "ZombieEstateInputCompat");
        var tryGetMouseAim = compatType.Methods.First(m => m.Name == "TryGetMouseAim");

        if (tryGetMouseAim.Body.Instructions.Any(i =>
                i.OpCode == OpCodes.Ldstr && i.Operand as string == "ZOMBIE_ESTATE_KEYBOARD_P2")
            && tryGetMouseAim.Body.Instructions.Any(i =>
                i.OpCode == OpCodes.Ldstr && i.Operand as string == "0"))
            return;

        PrependBlockP1MouseWhenCoop(tryGetMouseAim, module, false);

        WriteAssemblySafely(asm, path);
        Console.WriteLine("Patched FNA.NetStub coop mouse block in " + path);
    }

    static void WriteAssemblySafely(AssemblyDefinition asm, string path) {
        string temp = path + ".patchtmp";
        if (File.Exists(temp))
            File.Delete(temp);
        try {
            asm.Write(temp);
            File.Copy(temp, path, true);
        } finally {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    static void PrependBlockP1MouseWhenCoop(MethodDefinition method, ModuleDefinition module, bool returnFloat) {
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var skip = il.Create(OpCodes.Nop);
        var envGet = module.ImportReference(
            typeof(Environment).GetMethod("GetEnvironmentVariable", new[] { typeof(string) }));

        // Block mouse aim for PlayerIndex.One (0) unless solo keyboard (KEYBOARD_P2=0).
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, skip));
        il.InsertBefore(first, il.Create(OpCodes.Ldstr, "ZOMBIE_ESTATE_KEYBOARD_P2"));
        il.InsertBefore(first, il.Create(OpCodes.Call, envGet));
        il.InsertBefore(first, il.Create(OpCodes.Ldstr, "0"));
        il.InsertBefore(first, il.Create(OpCodes.Call,
            module.ImportReference(typeof(string).GetMethod("op_Equality", new[] { typeof(string), typeof(string) }))));
        il.InsertBefore(first, il.Create(OpCodes.Brtrue_S, skip));
        if (returnFloat)
            il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, 0f));
        else
            il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_0));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, skip);
    }

    static bool CompileAiDll(string source, string modsSource, string dualSource, string output, string gameExe, string fna) {
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        string mcs = FindMcs();
        string args = string.Format(
            "-target:library -out:\"{0}\" -r:\"{1}\" -r:\"{2}\" \"{3}\" \"{4}\" \"{5}\"",
            output, fna, gameExe, source, modsSource, dualSource);
        var psi = new ProcessStartInfo(mcs, args) {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using (var proc = Process.Start(psi)) {
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0) {
                Console.Error.WriteLine("Failed to compile AiTeammate.cs:");
                if (!string.IsNullOrEmpty(stdout)) Console.Error.WriteLine(stdout);
                if (!string.IsNullOrEmpty(stderr)) Console.Error.WriteLine(stderr);
                return false;
            }
        }
        return File.Exists(output);
    }

    static string FindMcs() {
        foreach (var candidate in new[] {
            "/Library/Frameworks/Mono.framework/Versions/Current/bin/mcs",
            "mcs"
        }) {
            if (File.Exists(candidate) || candidate == "mcs")
                return candidate;
        }
        return "mcs";
    }

    static void InjectType(ModuleDefinition targetModule, string helperDll) {
        var helper = AssemblyDefinition.ReadAssembly(helperDll);
        var helperModule = helper.MainModule;

        foreach (var fullName in new[] {
            "ZombieEstate.AiTeammate",
            "ZombieEstate.AiPlayerState",
            "ZombieEstate.GameMods",
            "ZombieEstate.DualScreen" }) {
            var existing = targetModule.Types.FirstOrDefault(t => t.FullName == fullName);
            if (existing != null)
                targetModule.Types.Remove(existing);
        }

        var typeMap = new Dictionary<TypeDefinition, TypeDefinition>();
        var fieldMap = new Dictionary<FieldDefinition, FieldDefinition>();
        var methodMap = new Dictionary<MethodDefinition, MethodDefinition>();

        foreach (var typeName in new[] { "AiPlayerState", "AiTeammate", "GameMods", "DualScreen" }) {
            var sourceType = helperModule.Types.First(t => t.Name == typeName);
            InjectSingleType(targetModule, sourceType, typeMap, fieldMap, methodMap);
        }

        var aiRef = targetModule.AssemblyReferences.FirstOrDefault(r => r.Name == "AiTeammate");
        if (aiRef != null)
            targetModule.AssemblyReferences.Remove(aiRef);
    }

    static TypeReference ImportType(
        ModuleDefinition targetModule,
        TypeReference source,
        Dictionary<TypeDefinition, TypeDefinition> typeMap) {
        if (source == null)
            return null;

        if (source is TypeDefinition td) {
            if (typeMap != null && typeMap.TryGetValue(td, out var mappedDef))
                return mappedDef;
        } else if (typeMap != null) {
            foreach (var entry in typeMap) {
                if (entry.Key.FullName == source.FullName)
                    return entry.Value;
            }
        }

        if (source is ArrayType arr)
            return new ArrayType(ImportType(targetModule, arr.ElementType, typeMap));
        if (source is ByReferenceType byRef)
            return new ByReferenceType(ImportType(targetModule, byRef.ElementType, typeMap));

        return targetModule.ImportReference(source);
    }

    static void InjectSingleType(
        ModuleDefinition targetModule,
        TypeDefinition sourceType,
        Dictionary<TypeDefinition, TypeDefinition> typeMap,
        Dictionary<FieldDefinition, FieldDefinition> fieldMap,
        Dictionary<MethodDefinition, MethodDefinition> methodMap) {
        var injected = new TypeDefinition(
            sourceType.Namespace,
            sourceType.Name,
            sourceType.Attributes,
            ImportType(targetModule, sourceType.BaseType, typeMap));

        typeMap[sourceType] = injected;

        foreach (var field in sourceType.Fields) {
            var nf = new FieldDefinition(field.Name, field.Attributes, ImportType(targetModule, field.FieldType, typeMap));
            if (field.HasConstant)
                nf.Constant = field.Constant;
            injected.Fields.Add(nf);
            fieldMap[field] = nf;
        }

        foreach (var method in sourceType.Methods) {
            if (method.IsConstructor)
                continue;
            var nm = CreateMethodShell(method, targetModule, typeMap);
            if (method.HasBody)
                CloneBody(method, nm, targetModule, fieldMap, methodMap, typeMap);
            injected.Methods.Add(nm);
            methodMap[method] = nm;
        }

        targetModule.Types.Add(injected);
    }

    static MethodDefinition CreateMethodShell(
        MethodDefinition source,
        ModuleDefinition targetModule,
        Dictionary<TypeDefinition, TypeDefinition> typeMap) {
        var method = new MethodDefinition(
            source.Name,
            source.Attributes,
            ImportType(targetModule, source.ReturnType, typeMap));
        method.ImplAttributes = source.ImplAttributes;
        foreach (var gp in source.GenericParameters)
            method.GenericParameters.Add(new GenericParameter(gp.Name, method));
        foreach (var param in source.Parameters)
            method.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, ImportType(targetModule, param.ParameterType, typeMap)));
        return method;
    }

    static FieldDefinition CloneField(FieldDefinition source, ModuleDefinition targetModule) {
        var field = new FieldDefinition(source.Name, source.Attributes, targetModule.ImportReference(source.FieldType));
        if (source.HasConstant)
            field.Constant = source.Constant;
        return field;
    }

    static MethodDefinition CloneMethod(MethodDefinition source, ModuleDefinition targetModule) {
        var method = CreateMethodShell(source, targetModule, null);
        if (source.HasBody)
            CloneBody(source, method, targetModule, null, null, null);
        return method;
    }

    static void CloneBody(
        MethodDefinition source,
        MethodDefinition target,
        ModuleDefinition targetModule,
        Dictionary<FieldDefinition, FieldDefinition> fieldMap,
        Dictionary<MethodDefinition, MethodDefinition> methodMap,
        Dictionary<TypeDefinition, TypeDefinition> typeMap) {
        var body = source.Body;
        target.Body = new MethodBody(target);
        target.Body.InitLocals = body.InitLocals;
        target.Body.MaxStackSize = body.MaxStackSize;

        var map = new Dictionary<Instruction, Instruction>();
        foreach (var local in body.Variables) {
            target.Body.Variables.Add(new VariableDefinition(ImportType(targetModule, local.VariableType, typeMap)));
        }

        var il = target.Body.GetILProcessor();
        foreach (var instr in body.Instructions) {
            var ni = CloneInstruction(il, instr, target, targetModule, map, fieldMap, methodMap, typeMap);
            map[instr] = ni;
            il.Append(ni);
        }

        foreach (var instr in body.Instructions) {
            var ni = map[instr];
            if (instr.Operand is Instruction targetInstr)
                ni.Operand = map[targetInstr];
            else if (instr.Operand is Instruction[] targets)
                ni.Operand = targets.Select(t => map[t]).ToArray();
        }

        foreach (var handler in body.ExceptionHandlers) {
            target.Body.ExceptionHandlers.Add(new ExceptionHandler(handler.HandlerType) {
                TryStart = map[handler.TryStart],
                TryEnd = map[handler.TryEnd],
                HandlerStart = map[handler.HandlerStart],
                HandlerEnd = map[handler.HandlerEnd],
                CatchType = handler.CatchType != null ? ImportType(targetModule, handler.CatchType, typeMap) : null,
                FilterStart = handler.FilterStart != null ? map[handler.FilterStart] : null
            });
        }
    }

    static Instruction CloneInstruction(
        ILProcessor il,
        Instruction source,
        MethodDefinition target,
        ModuleDefinition targetModule,
        Dictionary<Instruction, Instruction> map,
        Dictionary<FieldDefinition, FieldDefinition> fieldMap,
        Dictionary<MethodDefinition, MethodDefinition> methodMap,
        Dictionary<TypeDefinition, TypeDefinition> typeMap) {
        Instruction ni;
        switch (source.OpCode.OperandType) {
        case OperandType.InlineNone:
            ni = Instruction.Create(source.OpCode);
            break;
        case OperandType.ShortInlineBrTarget:
        case OperandType.InlineBrTarget:
            ni = Instruction.Create(source.OpCode, Instruction.Create(OpCodes.Nop));
            break;
        case OperandType.InlineSwitch:
            ni = Instruction.Create(source.OpCode, new Instruction[((Instruction[])source.Operand).Length]);
            break;
        case OperandType.ShortInlineVar:
            ni = Instruction.Create(source.OpCode, target.Body.Variables[((VariableDefinition)source.Operand).Index]);
            break;
        case OperandType.InlineVar:
            ni = Instruction.Create(source.OpCode, target.Body.Variables[((VariableDefinition)source.Operand).Index]);
            break;
        case OperandType.ShortInlineArg:
            ni = Instruction.Create(source.OpCode, target.Parameters[((ParameterDefinition)source.Operand).Index + (target.HasThis ? 1 : 0)]);
            break;
        case OperandType.InlineArg:
            ni = Instruction.Create(source.OpCode, target.Parameters[((ParameterDefinition)source.Operand).Index + (target.HasThis ? 1 : 0)]);
            break;
        case OperandType.InlineField:
            ni = Instruction.Create(source.OpCode, (FieldReference)ImportOperand(targetModule, source.Operand, fieldMap, methodMap, typeMap));
            break;
        case OperandType.InlineMethod:
            ni = Instruction.Create(source.OpCode, (MethodReference)ImportOperand(targetModule, source.Operand, fieldMap, methodMap, typeMap));
            break;
        case OperandType.InlineType:
            ni = Instruction.Create(source.OpCode, (TypeReference)ImportOperand(targetModule, source.Operand, fieldMap, methodMap, typeMap));
            break;
        case OperandType.InlineString:
            ni = Instruction.Create(source.OpCode, (string)source.Operand);
            break;
        case OperandType.ShortInlineI:
            ni = Instruction.Create(source.OpCode, (sbyte)source.Operand);
            break;
        case OperandType.InlineI:
            ni = Instruction.Create(source.OpCode, (int)source.Operand);
            break;
        case OperandType.InlineI8:
            ni = Instruction.Create(source.OpCode, (long)source.Operand);
            break;
        case OperandType.ShortInlineR:
            ni = Instruction.Create(source.OpCode, (float)source.Operand);
            break;
        case OperandType.InlineR:
            ni = Instruction.Create(source.OpCode, (double)source.Operand);
            break;
        case OperandType.InlineSig:
            ni = Instruction.Create(source.OpCode, (CallSite)source.Operand);
            break;
        case OperandType.InlineTok:
            var tok = ImportOperand(targetModule, source.Operand, fieldMap, methodMap, typeMap);
            if (tok is MethodReference)
                ni = Instruction.Create(source.OpCode, (MethodReference)tok);
            else if (tok is FieldReference)
                ni = Instruction.Create(source.OpCode, (FieldReference)tok);
            else
                ni = Instruction.Create(source.OpCode, (TypeReference)tok);
            break;
        default:
            throw new NotSupportedException("Unsupported opcode operand: " + source.OpCode.OperandType);
        }
        return ni;
    }

    static object ImportOperand(
        ModuleDefinition module,
        object operand,
        Dictionary<FieldDefinition, FieldDefinition> fieldMap,
        Dictionary<MethodDefinition, MethodDefinition> methodMap,
        Dictionary<TypeDefinition, TypeDefinition> typeMap) {
        if (operand == null)
            return null;
        if (operand is FieldReference fr) {
            if (fieldMap != null && fr.DeclaringType != null) {
                foreach (var entry in fieldMap) {
                    if (entry.Key.Name == fr.Name && entry.Key.DeclaringType.Name == fr.DeclaringType.Name)
                        return entry.Value;
                }
            }
            return module.ImportReference(fr);
        }
        if (operand is MethodReference mr) {
            if (methodMap != null && mr.DeclaringType != null) {
                foreach (var entry in methodMap) {
                    if (entry.Key.Name == mr.Name && entry.Key.HasParameters == mr.HasParameters
                        && entry.Key.DeclaringType.Name == mr.DeclaringType.Name)
                        return entry.Value;
                }
            }
            return module.ImportReference(mr);
        }
        if (operand is TypeReference tr)
            return ImportType(module, tr, typeMap);
        if (operand is ParameterDefinition pd)
            return pd;
        return operand;
    }

    static void PatchUpdateInputs(TypeDefinition inputType, TypeDefinition aiType) {
        var method = inputType.Methods.First(m => m.Name == "UpdateInputs");
        var tick = aiType.Methods.First(m => m.Name == "Tick" && m.IsStatic);
        var skipDisconnect = aiType.Methods.First(m => m.Name == "ShouldSkipDisconnect" && m.IsStatic);

        var il = method.Body.GetILProcessor();
        if (method.Body.Instructions.Any(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference mr && mr.Name == "Tick" && mr.DeclaringType.Name == "AiTeammate")) {
            Console.WriteLine("InputManager.UpdateInputs already patched.");
            return;
        }
        var first = method.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Call, tick));

        // Skip virtual-controller disconnect pause for AI player 2.
        var instructions = method.Body.Instructions;
        for (int i = 0; i < instructions.Count; i++) {
            var called = instructions[i].Operand as MethodReference;
            if (called == null || called.Name != "get_IsConnected")
                continue;
            if (instructions[i].OpCode != OpCodes.Call && instructions[i].OpCode != OpCodes.Callvirt)
                continue;

            var connectedBranch = instructions[i + 3];
            if (connectedBranch.OpCode != OpCodes.Brtrue_S && connectedBranch.OpCode != OpCodes.Brtrue)
                continue;

            var loopTail = (Instruction)connectedBranch.Operand;
            var insertPoint = instructions[i - 1];

            il.InsertBefore(insertPoint, il.Create(OpCodes.Ldloc_0));
            il.InsertBefore(insertPoint, il.Create(OpCodes.Call, skipDisconnect));
            il.InsertBefore(insertPoint, il.Create(OpCodes.Brtrue_S, loopTail));
            Console.WriteLine("Patched InputManager.UpdateInputs disconnect bypass.");
            break;
        }
    }

    static void PatchBoolPlayerIndex(
        TypeDefinition inputType,
        TypeDefinition aiType,
        string methodName,
        string queryName,
        string gamepadName,
        bool needsInputManager) {
        var method = inputType.Methods.First(m => m.Name == methodName && !m.HasGenericParameters);
        var query = aiType.Methods.First(m => m.Name == queryName && m.IsStatic);
        var gamepad = aiType.Methods.First(m => m.Name == gamepadName && m.IsStatic);
        if (MethodAlreadyAiPatched(method))
            return;

        PrependAiBoolQuery(method, aiType, query, gamepad, needsInputManager);
        Console.WriteLine("Patched InputManager." + methodName);
    }

    static void PatchAimAngle(TypeDefinition inputType, TypeDefinition aiType) {
        var method = inputType.Methods.First(m => m.Name == "GetRightStickAngle" && !m.HasGenericParameters);
        var hasAim = aiType.Methods.First(m => m.Name == "HasAimControl" && m.IsStatic);
        var getAim = aiType.Methods.First(m => m.Name == "GetAimAngle" && m.IsStatic);
        if (MethodAlreadyAiPatched(method))
            return;

        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var skip = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Call, hasAim));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, skip));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Call, getAim));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, skip);
        Console.WriteLine("Patched InputManager.GetRightStickAngle");
    }

    static void PatchBlockCompatInputForP1(TypeDefinition inputType, TypeDefinition aiType) {
        var block = aiType.Methods.First(m => m.Name == "KeyboardP2BlocksCompatInput" && m.IsStatic);
        var aimBool = aiType.Methods.First(m => m.Name == "GamepadAiming" && m.IsStatic);
        var aimAngle = aiType.Methods.First(m => m.Name == "GamepadAimAngle" && m.IsStatic);

        var aiming = inputType.Methods.First(m => m.Name == "Aiming" && !m.HasGenericParameters);
        if (!MethodAlreadyCompatBlocked(aiming)) {
            PrependGamepadWhenKeyboardP2BlocksCompat(aiming, block, aimBool, false);
            Console.WriteLine("Patched InputManager.Aiming for keyboard P2 P1 gamepad aim.");
        }

        var angle = inputType.Methods.First(m => m.Name == "GetRightStickAngle" && !m.HasGenericParameters);
        if (!MethodAlreadyCompatBlocked(angle)) {
            PrependGamepadWhenKeyboardP2BlocksCompat(angle, block, aimAngle, true);
            Console.WriteLine("Patched InputManager.GetRightStickAngle for keyboard P2 P1 gamepad aim.");
        }
    }

    static bool MethodAlreadyCompatBlocked(MethodDefinition method) {
        if (method.Body == null || method.Body.Instructions.Count == 0)
            return false;
        foreach (var instr in method.Body.Instructions) {
            if (instr.OpCode != OpCodes.Call || !(instr.Operand is MethodReference mr))
                continue;
            if (mr.Name == "KeyboardP2BlocksCompatInput")
                return true;
        }
        return false;
    }

    static void PrependGamepadWhenKeyboardP2BlocksCompat(
        MethodDefinition method,
        MethodReference block,
        MethodReference gamepad,
        bool returnFloat) {
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var skip = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Call, block));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, skip));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Call, gamepad));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, skip);
    }

    static bool MethodAlreadyGamepadOnlyRouted(MethodDefinition method) {
        if (method.Body == null || method.Body.Instructions.Count == 0)
            return false;
        foreach (var instr in method.Body.Instructions) {
            if (instr.OpCode != OpCodes.Call || !(instr.Operand is MethodReference mr))
                continue;
            if (mr.Name == "KeyboardP2UseGamepadOnly")
                return true;
        }
        return false;
    }

    static void PatchBlockNativeKeyboardForP2(TypeDefinition inputType, TypeDefinition aiType) {
        var enabled = aiType.Methods.First(m => m.Name == "KeyboardP2Active" && m.IsStatic);
        foreach (var name in new[] { "Held", "Pressed", "MouseLeftPressed", "MouseLeftHeld" }) {
            var method = inputType.Methods.First(m => m.Name == name && !m.HasGenericParameters);
            if (MethodAlreadyKeyboardBlocked(method))
                continue;
            PrependFalseWhenKeyboardP2(method, enabled);
            Console.WriteLine("Patched InputManager." + name + " for keyboard P2.");
        }
    }

    static bool MethodAlreadyKeyboardBlocked(MethodDefinition method) {
        if (method.Body == null || method.Body.Instructions.Count == 0)
            return false;
        foreach (var instr in method.Body.Instructions) {
            if (instr.OpCode != OpCodes.Call || !(instr.Operand is MethodReference mr))
                continue;
            if (mr.Name == "KeyboardP2Active")
                return true;
        }
        return false;
    }

    static void PrependFalseWhenKeyboardP2(MethodDefinition method, MethodReference enabled) {
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var skip = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Call, enabled));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, skip));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_0));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, skip);
    }

    static bool MethodAlreadyAiPatched(MethodDefinition method) {
        if (method.Body == null || method.Body.Instructions.Count == 0)
            return false;
        foreach (var instr in method.Body.Instructions) {
            if (instr.OpCode != OpCodes.Call || !(instr.Operand is MethodReference mr))
                continue;
            if (mr.DeclaringType != null && mr.DeclaringType.Name == "AiTeammate")
                return true;
        }
        return false;
    }

    static void PrependAiBoolQuery(
        MethodDefinition method,
        TypeDefinition aiType,
        MethodReference query,
        MethodReference gamepad,
        bool needsInputManager) {
        var overrides = aiType.Methods.First(m => m.Name == "QueryOverridesGamepad" && m.IsStatic);
        var useGamepadOnly = aiType.Methods.First(m => m.Name == "KeyboardP2UseGamepadOnly" && m.IsStatic);
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var skipQuery = il.Create(OpCodes.Nop);
        var skipGamepad = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Call, useGamepadOnly));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, skipGamepad));
        if (needsInputManager) {
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        } else {
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        }
        il.InsertBefore(first, il.Create(OpCodes.Call, gamepad));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, skipGamepad);

        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Call, overrides));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, skipQuery));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Call, query));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, skipQuery);
    }

    static void PatchTryUpdateCameraP1Only(TypeDefinition gameWorldType, TypeDefinition aiType) {
        var method = gameWorldType.Methods.First(m => m.Name == "updateCamera");
        var dualType = gameWorldType.Module.Types.FirstOrDefault(t => t.Name == "DualScreen");

        if (dualType != null && !MethodAlreadyDualScreenCamera(method)) {
            var dualMain = dualType.Methods.First(m => m.Name == "TryUpdateMainCamera" && m.IsStatic);
            var il = method.Body.GetILProcessor();
            var first = method.Body.Instructions[0];
            var skipDual = il.Create(OpCodes.Nop);

            il.InsertBefore(first, il.Create(OpCodes.Call, dualMain));
            il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, skipDual));
            il.InsertBefore(first, il.Create(OpCodes.Ret));
            il.InsertBefore(first, skipDual);
            Console.WriteLine("Patched GameWorld.updateCamera for dual-screen P1 view.");
        }

        if (MethodAlreadyAiPatched(method))
            return;

        var tryP1Camera = aiType.Methods.First(m => m.Name == "TryUpdateCameraP1Only" && m.IsStatic);
        var il2 = method.Body.GetILProcessor();
        var first2 = method.Body.Instructions[0];
        var skip = il2.Create(OpCodes.Nop);

        il2.InsertBefore(first2, il2.Create(OpCodes.Call, tryP1Camera));
        il2.InsertBefore(first2, il2.Create(OpCodes.Brfalse_S, skip));
        il2.InsertBefore(first2, il2.Create(OpCodes.Ret));
        il2.InsertBefore(first2, skip);
        Console.WriteLine("Patched GameWorld.updateCamera for P1-only AI view.");
    }

    static bool MethodAlreadyDualScreenCamera(MethodDefinition method) {
        foreach (var instr in method.Body.Instructions) {
            if (instr.OpCode != OpCodes.Call || !(instr.Operand is MethodReference mr))
                continue;
            if (mr.Name == "TryUpdateMainCamera")
                return true;
        }
        return false;
    }

    static void PatchGame1DualScreenEndDraw(TypeDefinition game1Type) {
        var dualType = game1Type.Module.Types.FirstOrDefault(t => t.Name == "DualScreen");
        if (dualType == null)
            return;

        RemoveAfterMainDrawFromDraw(game1Type);

        var endDraw = game1Type.Methods.FirstOrDefault(m =>
            m.Name == "EndDraw" && m.Parameters.Count == 0);
        if (endDraw == null)
            endDraw = CreateGame1EndDrawOverride(game1Type);

        var module = game1Type.Module;
        var fnaRef = module.AssemblyReferences.First(r => r.Name == "FNA");
        var gameTypeRef = new TypeReference("Microsoft.Xna.Framework", "Game", module, fnaRef);
        var baseEndDraw = new MethodReference("EndDraw", module.TypeSystem.Void, gameTypeRef);
        baseEndDraw.HasThis = true;

        var prepareMainPresent = dualType.Methods.First(m => m.Name == "PrepareMainPresent" && m.IsStatic);
        var afterMainDraw = dualType.Methods.First(m => m.Name == "AfterMainDraw" && m.IsStatic);

        endDraw.Body = new MethodBody(endDraw);
        var il = endDraw.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Call, prepareMainPresent));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Call, baseEndDraw));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Call, afterMainDraw));
        il.Append(il.Create(OpCodes.Ret));
        Console.WriteLine("Patched Game1.EndDraw for dual-screen extra windows.");
    }

    static void RemoveAfterMainDrawFromDraw(TypeDefinition game1Type) {
        var draw = game1Type.Methods.FirstOrDefault(m =>
            m.Name == "Draw"
            && m.Parameters.Count == 1
            && m.Parameters[0].ParameterType.FullName.Contains("GameTime"));
        if (draw == null)
            return;

        var il = draw.Body.GetILProcessor();
        var instrs = draw.Body.Instructions;
        for (int i = 0; i < instrs.Count; i++) {
            if (instrs[i].OpCode != OpCodes.Call || !(instrs[i].Operand is MethodReference mr))
                continue;
            if (mr.Name != "AfterMainDraw")
                continue;
            if (i > 0 && instrs[i - 1].OpCode == OpCodes.Ldarg_0)
                il.Remove(instrs[i - 1]);
            il.Remove(instrs[i]);
            Console.WriteLine("Removed legacy AfterMainDraw hook from Game1.Draw.");
            return;
        }
    }

    static MethodDefinition CreateGame1EndDrawOverride(TypeDefinition game1Type) {
        var endDraw = new MethodDefinition(
            "EndDraw",
            MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot,
            game1Type.Module.TypeSystem.Void);
        game1Type.Methods.Add(endDraw);
        return endDraw;
    }

    static void PatchGame1DualScreenHudFilter(TypeDefinition game1Type) {
        var dualType = game1Type.Module.Types.FirstOrDefault(t => t.Name == "DualScreen");
        if (dualType == null)
            return;

        var method = game1Type.Methods.First(m =>
            m.Name == "Draw"
            && m.Parameters.Count == 1
            && m.Parameters[0].ParameterType.FullName.Contains("GameTime"));
        if (MethodAlreadyDualScreenHudFiltered(method))
            return;

        Instruction drawHudCall = null;
        VariableDefinition playerVar = null;
        var instrs = method.Body.Instructions;
        for (int i = 0; i < instrs.Count; i++) {
            if (instrs[i].OpCode != OpCodes.Callvirt || !(instrs[i].Operand is MethodReference mr))
                continue;
            if (mr.Name != "DrawHUD" || mr.DeclaringType.Name != "GunCache")
                continue;
            drawHudCall = instrs[i];
            for (int j = i - 1; j >= 0 && j >= i - 8; j--) {
                if (instrs[j].OpCode != OpCodes.Ldfld || !(instrs[j].Operand is FieldReference fr))
                    continue;
                if (fr.Name != "gunCache")
                    continue;
                if (j > 0)
                    playerVar = GetLoadLocalVariable(instrs[j - 1], method);
                break;
            }
            break;
        }

        if (drawHudCall == null || playerVar == null)
            return;

        var shouldDraw = dualType.Methods.First(m => m.Name == "ShouldShowHudOnMainScreen" && m.IsStatic);
        var il = method.Body.GetILProcessor();
        var skipHud = il.Create(OpCodes.Nop);

        il.InsertBefore(drawHudCall, il.Create(OpCodes.Ldloc, playerVar));
        il.InsertBefore(drawHudCall, il.Create(OpCodes.Call, shouldDraw));
        il.InsertBefore(drawHudCall, il.Create(OpCodes.Brtrue_S, drawHudCall));
        il.InsertBefore(drawHudCall, il.Create(OpCodes.Pop));
        il.InsertBefore(drawHudCall, il.Create(OpCodes.Pop));
        il.InsertBefore(drawHudCall, il.Create(OpCodes.Br_S, skipHud));
        il.InsertAfter(drawHudCall, skipHud);
        Console.WriteLine("Patched Game1.Draw HUD filter for dual-screen.");
    }

    static void PatchGame1DualScreenShopFilter(TypeDefinition game1Type) {
        var dualType = game1Type.Module.Types.FirstOrDefault(t => t.Name == "DualScreen");
        if (dualType == null)
            return;

        var method = game1Type.Methods.First(m =>
            m.Name == "Draw"
            && m.Parameters.Count == 1
            && m.Parameters[0].ParameterType.FullName.Contains("GameTime"));
        if (MethodAlreadyDualScreenShopFiltered(method))
            return;

        RepairGame1DrawShopFilter(game1Type);
        if (MethodAlreadyDualScreenShopFiltered(method))
            return;

        Instruction drawStoreCall = null;
        foreach (var instr in method.Body.Instructions) {
            if (instr.OpCode != OpCodes.Callvirt || !(instr.Operand is MethodReference mr))
                continue;
            if (mr.Name == "DrawStore" && mr.DeclaringType.Name == "Store") {
                drawStoreCall = instr;
                break;
            }
        }
        if (drawStoreCall == null)
            return;

        // Original stack before DrawStore: store, spriteBatch
        // DrawMainScreenShopUi needs: game1, spriteBatch
        Instruction spriteBatchLoad = drawStoreCall.Previous;
        if (spriteBatchLoad == null || spriteBatchLoad.OpCode != OpCodes.Ldfld)
            return;

        Instruction game1ForBatch = spriteBatchLoad.Previous;
        if (game1ForBatch == null || game1ForBatch.OpCode != OpCodes.Ldarg_0)
            return;

        Instruction testStoreLoad = game1ForBatch.Previous;
        if (testStoreLoad == null || testStoreLoad.OpCode != OpCodes.Ldfld)
            return;

        Instruction game1ForStore = testStoreLoad.Previous;
        if (game1ForStore == null || game1ForStore.OpCode != OpCodes.Ldarg_0)
            return;

        var drawMainShop = dualType.Methods.First(m => m.Name == "DrawMainScreenShopUi" && m.IsStatic);
        var il = method.Body.GetILProcessor();
        il.Remove(game1ForStore);
        il.Remove(testStoreLoad);
        il.Remove(game1ForBatch);
        // ldfld consumes its object; push game1 twice so call gets (game1, spriteBatch).
        il.InsertBefore(spriteBatchLoad, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(spriteBatchLoad, il.Create(OpCodes.Ldarg_0));
        il.Replace(drawStoreCall, il.Create(OpCodes.Call, drawMainShop));
        Console.WriteLine("Patched Game1.Draw shop filter for dual-screen.");
    }

    static bool MethodAlreadyDualScreenShopFiltered(MethodDefinition method) {
        foreach (var instr in method.Body.Instructions) {
            if (instr.OpCode != OpCodes.Call || !(instr.Operand is MethodReference mr))
                continue;
            if (mr.Name != "DrawMainScreenShopUi")
                continue;

            Instruction batchLoad = instr.Previous;
            if (batchLoad == null || batchLoad.OpCode != OpCodes.Ldfld)
                return false;

            Instruction game1Load = batchLoad.Previous;
            if (game1Load == null || game1Load.OpCode != OpCodes.Ldarg_0)
                return false;

            Instruction game1Dup = game1Load.Previous;
            return game1Dup != null && game1Dup.OpCode == OpCodes.Ldarg_0;
        }
        return false;
    }

    static void RepairGame1DrawShopFilter(TypeDefinition game1Type) {
        var dualType = game1Type.Module.Types.FirstOrDefault(t => t.Name == "DualScreen");
        if (dualType == null)
            return;

        var method = game1Type.Methods.First(m =>
            m.Name == "Draw"
            && m.Parameters.Count == 1
            && m.Parameters[0].ParameterType.FullName.Contains("GameTime"));
        if (MethodAlreadyDualScreenShopFiltered(method))
            return;

        Instruction shopCall = null;
        foreach (var instr in method.Body.Instructions) {
            if (instr.OpCode != OpCodes.Call || !(instr.Operand is MethodReference mr))
                continue;
            if (mr.Name != "DrawMainScreenShopUi")
                continue;
            shopCall = instr;
            break;
        }
        if (shopCall == null)
            return;

        Instruction batchLoad = shopCall.Previous;
        if (batchLoad == null || batchLoad.OpCode != OpCodes.Ldfld)
            return;

        Instruction game1Load = batchLoad.Previous;
        if (game1Load == null || game1Load.OpCode != OpCodes.Ldarg_0)
            return;

        if (game1Load.Previous != null && game1Load.Previous.OpCode == OpCodes.Ldarg_0)
            return;

        var il = method.Body.GetILProcessor();
        il.InsertBefore(batchLoad, il.Create(OpCodes.Ldarg_0));
        Console.WriteLine("Repaired Game1.Draw shop filter stack for dual-screen.");
    }

    static VariableDefinition GetLoadLocalVariable(Instruction instr, MethodDefinition method) {
        if (instr.OpCode == OpCodes.Ldloc_0) return method.Body.Variables[0];
        if (instr.OpCode == OpCodes.Ldloc_1) return method.Body.Variables[1];
        if (instr.OpCode == OpCodes.Ldloc_2) return method.Body.Variables[2];
        if (instr.OpCode == OpCodes.Ldloc_3) return method.Body.Variables[3];
        if (instr.OpCode == OpCodes.Ldloc_S || instr.OpCode == OpCodes.Ldloc)
            return instr.Operand as VariableDefinition;
        return null;
    }

    static bool MethodAlreadyDualScreenHudFiltered(MethodDefinition method) {
        foreach (var instr in method.Body.Instructions) {
            if (instr.OpCode != OpCodes.Call || !(instr.Operand is MethodReference mr))
                continue;
            if (mr.Name == "ShouldShowHudOnMainScreen")
                return true;
        }
        return false;
    }

    static bool MethodAlreadyDualScreenEndDraw(MethodDefinition method) {
        foreach (var instr in method.Body.Instructions) {
            if (instr.OpCode != OpCodes.Call || !(instr.Operand is MethodReference mr))
                continue;
            if (mr.Name == "AfterMainDraw")
                return true;
        }
        return false;
    }

    static void PatchMaxDistanceForAi(TypeDefinition gameWorldType, TypeDefinition aiType) {
        var method = gameWorldType.Methods.First(m => m.Name == "MaxDistance");
        if (MethodAlreadyAiPatched(method))
            return;

        var useP1Only = aiType.Methods.First(m => m.Name == "UseP1OnlyCamera" && m.IsStatic);
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var skip = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Call, useP1Only));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, skip));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, 10f));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, skip);
        Console.WriteLine("Patched GameWorld.MaxDistance for AI P2.");
    }

    static void PatchSkipCameraBounds(TypeDefinition playerType, TypeDefinition aiType) {
        var method = playerType.Methods.First(m => m.Name == "checkCameraBounds");
        if (MethodAlreadyAiPatched(method))
            return;

        var skipBounds = aiType.Methods.First(m => m.Name == "SkipCameraBounds" && m.IsStatic);
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var skip = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Call, skipBounds));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, skip));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, skip);
        Console.WriteLine("Patched Player.checkCameraBounds for AI P2.");
    }

    static void PatchGameWorldMapFiles(TypeDefinition gameWorldType, TypeDefinition modsType) {
        var getLevel = modsType.Methods.First(m => m.Name == "GetLevelFile" && m.IsStatic);
        var getPath = modsType.Methods.First(m => m.Name == "GetPathMapFile" && m.IsStatic);
        var getSpawns = modsType.Methods.First(m => m.Name == "GetSpawnsFile" && m.IsStatic);
        var ctor = gameWorldType.Methods.First(m => m.Name == ".ctor" && m.Parameters.Count == 3);
        if (MethodAlreadyAiPatched(ctor))
            return;

        var il = ctor.Body.GetILProcessor();
        foreach (var instr in ctor.Body.Instructions.ToList()) {
            if (instr.OpCode != OpCodes.Ldstr)
                continue;
            var text = instr.Operand as string;
            MethodReference repl = null;
            if (text == "Manor.txt")
                repl = getLevel;
            else if (text == "PathMap_Manor.txt")
                repl = getPath;
            else if (text == "ManorSpawns.txt")
                repl = getSpawns;
            if (repl == null)
                continue;
            il.Replace(instr, il.Create(OpCodes.Call, repl));
        }
        Console.WriteLine("Patched GameWorld map file selection.");
    }

    static void PatchGame1LoadContent(TypeDefinition game1Type, TypeDefinition modsType) {
        var method = game1Type.Methods.First(m => m.Name == "LoadContent");
        var afterLoad = modsType.Methods.First(m => m.Name == "AfterLoadContent" && m.IsStatic);
        if (MethodAlreadyAiPatched(method))
            return;

        var il = method.Body.GetILProcessor();
        var ret = method.Body.Instructions.Last(i => i.OpCode == OpCodes.Ret);
        il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(ret, il.Create(OpCodes.Call, afterLoad));
        Console.WriteLine("Patched Game1.LoadContent for secondary store.");
    }

    static bool MethodAlreadyGameModsPatched(MethodDefinition method, string modsMethodName) {
        if (method.Body == null || method.Body.Instructions.Count == 0)
            return false;
        foreach (var instr in method.Body.Instructions) {
            if (instr.OpCode != OpCodes.Call || !(instr.Operand is MethodReference mr))
                continue;
            if (mr.DeclaringType != null && mr.DeclaringType.Name == "GameMods" && mr.Name == modsMethodName)
                return true;
        }
        return false;
    }

    static void PatchCharacterSelectionApplyMap(TypeDefinition charSelectType, TypeDefinition modsType) {
        var update = charSelectType.Methods.First(m => m.Name == "Update" && m.Parameters.Count == 1);
        var applyMap = modsType.Methods.First(m => m.Name == "ApplySelectedMap" && m.IsStatic);
        if (MethodAlreadyGameModsPatched(update, "ApplySelectedMap"))
            return;

        var il = update.Body.GetILProcessor();
        var instructions = update.Body.Instructions;
        for (int i = 0; i < instructions.Count; i++) {
            if (instructions[i].OpCode != OpCodes.Callvirt)
                continue;
            var called = instructions[i].Operand as MethodReference;
            if (called == null || called.Name != "ClearItems")
                continue;

            for (int j = i + 1; j < instructions.Count && j < i + 12; j++) {
                if (instructions[j].OpCode != OpCodes.Ldsfld)
                    continue;
                var field = instructions[j].Operand as FieldReference;
                if (field == null || field.Name != "World")
                    continue;
                il.InsertBefore(instructions[j], il.Create(OpCodes.Call, applyMap));
                Console.WriteLine("Patched CharacterSelection.Update for selected map load.");
                return;
            }
        }
        Console.Error.WriteLine("Could not find CharacterSelection start-game hook for map apply.");
    }

    static void PatchCharacterSelectionMapSelect(TypeDefinition charSelectType, TypeDefinition modsType) {
        var update = charSelectType.Methods.First(m => m.Name == "Update" && m.Parameters.Count == 1);
        var draw = charSelectType.Methods.First(m => m.Name == "DrawHUDS");
        var updateMap = modsType.Methods.First(m => m.Name == "UpdateMapSelect" && m.IsStatic);
        var drawLabel = modsType.Methods.First(m => m.Name == "DrawMapLabel" && m.IsStatic);

        if (!MethodAlreadyGameModsPatched(update, "UpdateMapSelect")) {
            var il = update.Body.GetILProcessor();
            var first = update.Body.Instructions[0];
            il.InsertBefore(first, il.Create(OpCodes.Call, updateMap));
            Console.WriteLine("Patched CharacterSelection.Update for map select.");
        }

        if (!MethodAlreadyGameModsPatched(draw, "DrawMapLabel")) {
            var il = draw.Body.GetILProcessor();
            var first = draw.Body.Instructions[0];
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(first, il.Create(OpCodes.Call, drawLabel));
            Console.WriteLine("Patched CharacterSelection.DrawHUDS for map label.");
        }
    }

    static void PatchSinglePlayerCoop(TypeDefinition charSelectType) {
        // CharacterSelection defaults to SINGLEPLAYER=true which blocks joining P2.
        var ctor = charSelectType.Methods.First(m => m.Name == ".ctor");
        foreach (var instr in ctor.Body.Instructions) {
            if (instr.OpCode != OpCodes.Stfld)
                continue;
            var field = instr.Operand as FieldReference;
            if (field == null || field.Name != "SINGLEPLAYER")
                continue;
            var prev = instr.Previous;
            if (prev != null && prev.OpCode == OpCodes.Ldc_I4_1)
                prev.OpCode = OpCodes.Ldc_I4_0;
            Console.WriteLine("Patched CharacterSelection.SINGLEPLAYER default for co-op.");
            return;
        }
    }

    static void PatchGame1CarnivalClowns(TypeDefinition game1Type, TypeDefinition modsType) {
        var update = game1Type.Methods.First(m => m.Name == "Update" && m.Parameters.Count == 1);
        var clownUpdate = modsType.Methods.First(m => m.Name == "UpdateCarnivalClowns" && m.IsStatic);
        if (MethodAlreadyGameModsPatched(update, "UpdateCarnivalClowns"))
            return;

        var il = update.Body.GetILProcessor();
        var first = update.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Call, clownUpdate));
        Console.WriteLine("Patched Game1.Update for carnival clown zombies.");
    }
}
