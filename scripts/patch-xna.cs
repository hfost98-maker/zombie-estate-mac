using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

class PatchXna {
    static void Main(string[] args) {
        string path = Path.GetFullPath(args[0]);
        var reader = new ReaderParameters {
            ReadingMode = ReadingMode.Deferred,
            ReadWrite = true,
            InMemory = true
        };

        var asm = AssemblyDefinition.ReadAssembly(path, reader);
        var module = asm.MainModule;

        foreach (var reference in module.AssemblyReferences.ToList()) {
            if (reference.Name.StartsWith("Microsoft.Xna.Framework")) {
                reference.Name = "FNA";
                reference.PublicKeyToken = null;
                reference.Version = new Version(0, 0, 0, 0);
            } else if (reference.Name == "mscorlib" || reference.Name == "System") {
                reference.Version = new Version(4, 0, 0, 0);
                reference.PublicKeyToken = null;
            }
        }

        var fnaRef = module.AssemblyReferences.First(r => r.Name == "FNA");
        AssemblyNameReference stubRef = null;
        if (File.Exists(Path.Combine(Path.GetDirectoryName(path), "FNA.NetStub.dll"))) {
            if (!module.AssemblyReferences.Any(r => r.Name == "FNA.NetStub")) {
                module.AssemblyReferences.Add(new AssemblyNameReference("FNA.NetStub", new Version(0, 0, 0, 0)));
            }
            stubRef = module.AssemblyReferences.First(r => r.Name == "FNA.NetStub");
        }

        foreach (var type in module.Types) {
            try {
                PatchType(type, fnaRef, stubRef);
            } catch (Exception ex) {
                Console.WriteLine("Skip " + type.FullName + ": " + ex.Message);
            }
        }

        asm.Write(path);
        Console.WriteLine("Patched " + path);
    }

    static void PatchType(TypeDefinition type, AssemblyNameReference fnaRef, AssemblyNameReference stubRef) {
        foreach (var nested in type.NestedTypes)
            PatchType(nested, fnaRef, stubRef);

        if (type.BaseType != null) {
            Relink(type.BaseType, fnaRef, stubRef);
            Console.WriteLine(type.Name + " : " + type.BaseType.FullName);
        }

        foreach (var field in type.Fields) {
            Relink(field.FieldType, fnaRef, stubRef);
        }
    }

    static void Relink(TypeReference tr, AssemblyNameReference fnaRef, AssemblyNameReference stubRef) {
        if (tr == null) return;
        var scopeName = (tr.Scope as AssemblyNameReference)?.Name;
        if (scopeName == null && tr.Scope != null) scopeName = tr.Scope.Name;
        if (scopeName == null) return;
        if (!(scopeName.StartsWith("Microsoft.Xna.Framework") || scopeName == "FNA" || scopeName == "FNA.NetStub"))
            return;

        var ns = tr.Namespace ?? "";
        tr.Scope = (stubRef != null && (ns.Contains("GamerServices") || ns.Contains(".Net"))) ? stubRef : fnaRef;

        if (tr is GenericInstanceType git)
            foreach (var arg in git.GenericArguments)
                Relink(arg, fnaRef, stubRef);
        if (tr is ArrayType arr)
            Relink(arr.ElementType, fnaRef, stubRef);
    }
}
