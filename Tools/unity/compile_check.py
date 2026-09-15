#!/usr/bin/env python3
"""
Compile-check the project's C# without opening Unity.

Uses the Roslyn compiler and reference assemblies bundled with the Unity editor that
ProjectSettings/ProjectVersion.txt names, plus the project's last-built assemblies in
Library/ScriptAssemblies for everything not recompiled here. Project assemblies
(Assets/_Project/**/*.asmdef, tests skipped) are compiled from source in dependency order,
so a change in a runtime assembly is seen by the editor code that uses it.

Usage:
    python3 Tools/unity/compile_check.py            # every project assembly
    python3 Tools/unity/compile_check.py HorrorUtez.World.Editor   # it and its project deps

Exit code 0 when everything compiles. Output goes to Library/CompileCheck (gitignored).
Unity's own compile is still the authority: this catches type and syntax errors early.
"""

import glob
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Library", "CompileCheck")


def editor_scripting():
    text = open(os.path.join(ROOT, "ProjectSettings", "ProjectVersion.txt")).read()
    version = re.search(r"m_EditorVersion:\s*(\S+)", text).group(1)
    path = f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/Resources/Scripting"
    if not os.path.isdir(path):
        sys.exit(f"Unity {version} not found at {path}")
    return path


def project_assemblies():
    asm = {}
    for path in glob.glob(os.path.join(ROOT, "Assets", "_Project", "**", "*.asmdef"), recursive=True):
        data = json.load(open(path))
        name = data["name"]
        if "Tests" in name:
            continue
        asm[name] = {"dir": os.path.dirname(path), "refs": data.get("references", [])}
    nested = [a["dir"] for a in asm.values()]
    for a in asm.values():
        own = []
        for cs in glob.glob(os.path.join(a["dir"], "**", "*.cs"), recursive=True):
            inner = [d for d in nested if d != a["dir"] and cs.startswith(d + os.sep)
                     and d.startswith(a["dir"] + os.sep)]
            if not inner:
                own.append(cs)
        a["sources"] = sorted(own)
    return asm


def order(asm, wanted):
    done, seq = set(), []

    def visit(name):
        if name in done or name not in asm:
            return
        done.add(name)
        for r in asm[name]["refs"]:
            visit(r)
        seq.append(name)

    for w in wanted:
        visit(w)
    return seq


def main():
    scripting = editor_scripting()
    dotnet = os.path.join(scripting, "DotNetSdk", "dotnet")
    csc = sorted(glob.glob(os.path.join(scripting, "DotNetSdk", "sdk", "*", "Roslyn", "bincore", "csc.dll")))[-1]
    api = os.path.join(scripting, "UnityReferenceAssemblies", "unity-4.8-api")
    base = (glob.glob(os.path.join(api, "*.dll")) + glob.glob(os.path.join(api, "Facades", "*.dll"))
            + glob.glob(os.path.join(scripting, "Managed", "UnityEngine", "*.dll")))

    asm = project_assemblies()
    seq = order(asm, sys.argv[1:] or sorted(asm))
    os.makedirs(OUT, exist_ok=True)
    for f in glob.glob(os.path.join(OUT, "*.dll")):
        os.remove(f)

    built = glob.glob(os.path.join(ROOT, "Library", "ScriptAssemblies", "*.dll"))
    stale = [d for d in built if os.path.splitext(os.path.basename(d))[0] not in seq
             and "Tests" not in d and "CodeGen" not in d]

    failed = False
    for name in seq:
        fresh = glob.glob(os.path.join(OUT, "*.dll"))
        rsp = os.path.join(OUT, name + ".rsp")
        with open(rsp, "w") as f:
            f.write("-nologo -noconfig -nostdlib -target:library -langversion:9 -warn:0\n")
            f.write("-define:UNITY_EDITOR;UNITY_6000_0_OR_NEWER;UNITY_STANDALONE_OSX\n")
            f.write(f'-out:"{os.path.join(OUT, name + ".dll")}"\n')
            for r in base + stale + fresh:
                f.write(f'-r:"{r}"\n')
            for s in asm[name]["sources"]:
                f.write(f'"{s}"\n')
        proc = subprocess.run([dotnet, csc, "@" + rsp], capture_output=True, text=True)
        errors = [l for l in proc.stdout.splitlines() if " error " in l]
        ok = proc.returncode == 0
        failed |= not ok
        print(("OK   " if ok else "FAIL ") + name)
        for line in errors[:40]:
            print("     " + line.replace(ROOT + os.sep, ""))
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
