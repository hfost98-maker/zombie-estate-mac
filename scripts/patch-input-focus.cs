using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class PatchInputFocus {
    static int Main(string[] args) {
        string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        string gameExe = Path.Combine(root, "game", "ZombieEstate.exe");
        string netStub = Path.Combine(root, "game", "FNA.NetStub.dll");
        string libStub = Path.Combine(root, "lib", "FNA.NetStub.dll");

        if (!File.Exists(gameExe)) {
            Console.Error.WriteLine("Missing " + gameExe);
            return 1;
        }

        PatchGameExe(gameExe);
        if (File.Exists(netStub))
            PatchNetStub(netStub);
        if (File.Exists(libStub))
            PatchNetStub(libStub);

        Console.WriteLine("Input focus patch applied.");
        return 0;
    }

    static void PatchGameExe(string path) {
        var asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters {
            ReadWrite = true,
            InMemory = true
        });
        var module = asm.MainModule;
        var inputType = module.Types.First(t => t.FullName == "ZombieEstate.InputManager");
        var globalType = module.Types.First(t => t.FullName == "ZombieEstate.Global");
        var gameField = globalType.Fields.First(f => f.Name == "Game");
        var gameType = module.ImportReference(globalType.Fields.First(f => f.Name == "Game").FieldType).Resolve();
        var isActive = module.ImportReference(gameType.Methods.First(m => m.Name == "get_IsActive"));

        foreach (var name in new[] { "MouseLeftPressed", "MouseRightPressed", "Pressed" }) {
            var method = inputType.Methods.First(m => m.Name == name && !m.HasGenericParameters);
            if (method.Body.Instructions.Any(i => i.OpCode == OpCodes.Callvirt && i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "get_IsActive"))
                continue;
            PrependFocusGuard(method, gameField, isActive);
            Console.WriteLine("Patched InputManager." + name);
        }

        asm.Write(path);
    }

    static void PatchNetStub(string path) {
        var asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters {
            ReadWrite = true,
            InMemory = true
        });
        var module = asm.MainModule;
        var compatType = module.Types.First(t => t.Name == "ZombieEstateInputCompat");
        var tryGetMouseAim = compatType.Methods.First(m => m.Name == "TryGetMouseAim");

        if (tryGetMouseAim.Body.Instructions.Any(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "HasInputFocus"))
            return;

        var hasFocus = compatType.Methods.FirstOrDefault(m => m.Name == "HasInputFocus");
        if (hasFocus == null)
            hasFocus = AddHasInputFocusMethod(module, compatType);

        PrependFocusGuardReturnFalse(tryGetMouseAim, hasFocus);
        Console.WriteLine("Patched FNA.NetStub TryGetMouseAim in " + path);
        asm.Write(path);
    }

    static MethodDefinition AddHasInputFocusMethod(ModuleDefinition module, TypeDefinition compatType) {
        var mscorlib = module.TypeSystem.Object.Resolve().Module;
        var intPtr = module.ImportReference(typeof(IntPtr));
        var sdlType = module.AssemblyReferences.Any(r => r.Name == "FNA")
            ? module.Types.FirstOrDefault(t => t.FullName == "SDL3.SDL") : null;

        // Fallback: use ZombieEstate.Global.Game via a lazy static we can't access.
        // Use FNA GamePlatform path: import SDL3.SDL from FNA assembly.
        var fnaModule = ModuleDefinition.ReadModule(FnaPath(module));
        var sdlRefType = fnaModule.Types.First(t => t.FullName == "SDL3.SDL");
        var getKeyboardFocus = fnaModule.Types.SelectMany(t => t.Methods)
            .First(m => m.Name == "SDL_GetKeyboardFocus");
        var importedGetFocus = module.ImportReference(getKeyboardFocus);

        var method = new MethodDefinition(
            "HasInputFocus",
            MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig,
            module.TypeSystem.Boolean);
        var il = method.Body.GetILProcessor();
        method.Body.InitLocals = true;
        var focusVar = new VariableDefinition(intPtr);
        method.Body.Variables.Add(focusVar);

        il.Append(il.Create(OpCodes.Call, importedGetFocus));
        il.Append(il.Create(OpCodes.Stloc_0));
        il.Append(il.Create(OpCodes.Ldloc_0));
        il.Append(il.Create(OpCodes.Ldc_I4_0));
        il.Append(il.Create(OpCodes.Conv_I));
        il.Append(il.Create(OpCodes.Call, module.ImportReference(typeof(IntPtr).GetMethod("op_Inequality", new[] { typeof(IntPtr), typeof(IntPtr) }))));
        il.Append(il.Create(OpCodes.Ret));
        compatType.Methods.Add(method);
        return method;
    }

    static string FnaPath(ModuleDefinition module) {
        string dir = Path.GetDirectoryName(module.FileName);
        foreach (var candidate in new[] {
            Path.Combine(dir, "FNA.dll"),
            Path.Combine(dir, "..", "lib", "FNA.dll"),
            Path.Combine(dir, "..", "FNA.dll")
        }) {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }
        throw new FileNotFoundException("FNA.dll not found near " + dir);
    }

    static void PrependFocusGuard(MethodDefinition method, FieldDefinition gameField, MethodReference isActive) {
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var skip = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Ldsfld, gameField));
        il.InsertBefore(first, il.Create(OpCodes.Callvirt, isActive));
        il.InsertBefore(first, il.Create(OpCodes.Brtrue_S, skip));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_0));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, skip);
    }

    static void PrependFocusGuardReturnFalse(MethodDefinition method, MethodDefinition hasFocus) {
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var skip = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Call, hasFocus));
        il.InsertBefore(first, il.Create(OpCodes.Brtrue_S, skip));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_0));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, skip);
    }
}
