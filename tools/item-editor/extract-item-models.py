"""Extrae de MuMain la tabla "indice de item -> archivo de modelo" (item-models.json).

MuMain asocia cada item a su .bmd en codigo C++ (OpenItems(), OpenPlayers(), etc.), no en
una tabla. Este script interpreta esas funciones (bucles for, if/else, constantes de
_define.h/_enum.h) y genera el JSON que usa el Item Editor.

Uso:  python extract-item-models.py <ruta al repo de MuMain>
      (por ejemplo la carpeta que deja scriptsuild-network-library.ps1 en client\_build\MuMain)
"""
import re, json, sys, os
sys.setrecursionlimit(20000)

if len(sys.argv) < 2 or not os.path.isdir(os.path.join(sys.argv[1], "src", "source")):
    print(__doc__, file=sys.stderr); sys.exit(1)
ROOT = os.path.join(sys.argv[1], "src", "source")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "item-models.json")

FUNCTIONS = [
    ("Engine/Object/ZzzOpenData.cpp", "void OpenItems()"),
    ("Engine/Object/ZzzOpenData.cpp", "void OpenPlayers()"),
    ("GameLogic/Social/MonkSystem.cpp", "void CMonkSystem::LoadModelItem()"),
    ("GameLogic/Items/ChangeRingManager.cpp", "void CChangeRingManager::LoadItemModel()"),
]

def function_body(rel, signature):
    src = open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace").read().splitlines()
    start = next(i for i, l in enumerate(src) if l.strip().startswith(signature))
    end = next(i for i in range(start, len(src)) if src[i].startswith("}"))
    return src[start:end + 1]
defs = {}

def clean_expr(e):
    e = re.sub(r"//.*", "", e)
    e = re.sub(r"/\*.*?\*/", "", e)
    e = e.replace("static_cast<int>", "").replace("(int)", "")
    e = e.replace("||", " or ").replace("&&", " and ").replace("!=", "<>").replace("!", " not ").replace("<>", "!=")
    e = e.strip().rstrip(",;")
    return e

cache = {}
class Env(dict):
    def __missing__(self, key):
        if key in cache: return cache[key]
        if key not in defs: raise KeyError(key)
        v = ev(defs[key])
        cache[key] = v
        return v

def ev(expr, local=None):
    env = Env()
    if local: env.update(local)
    try:
        return int(eval(expr, {"__builtins__": {}}, env))
    except Exception as e:
        raise ValueError(f"no puedo evaluar {expr!r}: {e}")


# --- #define y constexpr de todos los headers de Globals
for fn in ["_define.h", "_enum.h", "_struct.h"]:
    path = os.path.join(ROOT, "Core", "Globals", fn)
    src = open(path, encoding="utf-8", errors="replace").read()
    for m in re.finditer(r"^\s*#define\s+([A-Za-z_]\w*)\s+(.+)$", src, re.M):
        defs.setdefault(m.group(1), clean_expr(m.group(2)))
    for m in re.finditer(r"constexpr\s+(?:int|short|unsigned int|BYTE|WORD|DWORD)\s+([A-Za-z_]\w*)\s*=\s*([^;]+);", src):
        defs.setdefault(m.group(1), clean_expr(m.group(2)))
    # enums
    for m in re.finditer(r"enum\s+(?:class\s+)?\w*\s*(?::\s*\w+)?\s*\{(.*?)\}", src, re.S):
        body = re.sub(r"//.*", "", m.group(1))
        body = re.sub(r"/\*.*?\*/", "", body, flags=re.S)
        body = re.sub(r"#.*", "", body)
        prev_val = -1
        for item in body.split(","):
            item = item.strip()
            if not item: continue
            if "=" in item:
                name, val = item.split("=", 1)
                name = name.strip(); val = clean_expr(val)
                if name in defs: continue
                defs[name] = val
                try: prev_val = ev(val)
                except Exception: prev_val = None
                if prev_val is not None: cache[name] = prev_val
            else:
                name = item.strip()
                if not re.match(r"^[A-Za-z_]\w*$", name) or name in defs: continue
                if prev_val is None: defs[name] = "0"; continue
                prev_val += 1
                defs[name] = str(prev_val); cache[name] = prev_val

MODEL_ITEM = ev("MODEL_ITEM")
print("MODEL_ITEM =", MODEL_ITEM, file=sys.stderr)

lines = []
models = {}
class Continue(Exception): pass
def access(args, local):
    m = re.match(r"\s*(.+?)\s*,\s*(L\"[^\"]*\"|szPC6Path)\s*,\s*L\"([^\"]*)\"\s*(?:,\s*(.+))?$", args)
    if not m: raise ValueError(args)
    type_expr, dir_expr, fname, idx_expr = m.groups()
    t = ev(clean_expr(type_expr), local)
    d = "Data\\Item\\partCharge6\\" if dir_expr == "szPC6Path" else dir_expr[2:-1].replace("\\\\", "\\")
    if idx_expr is None: name = f"{fname}.bmd"
    else:
        i = ev(clean_expr(idx_expr), local)
        name = f"{fname}.bmd" if i == -1 else (f"{fname}0{i}.bmd" if i < 10 else f"{fname}{i}.bmd")
    idx = t - MODEL_ITEM
    if 0 <= idx < 8192:
        models[idx] = d + name

def run(i, local, depth=0):
    """Ejecuta la sentencia que empieza en la linea i. Devuelve la siguiente linea."""
    while i < len(lines) and lines[i].strip() == "": i += 1
    if i >= len(lines): return i
    s = lines[i].strip()
    if s == "{":
        i += 1
        while lines[i].strip() != "}":
            i = run(i, local, depth + 1)
        return i + 1
    m = re.match(r"for\s*\(\s*int\s+(\w+)\s*=\s*(.+?);\s*\1\s*(<=?)\s*(.+?);\s*(?:\1\+\+|\+\+\1)\s*\)\s*$", s)
    if m:
        var, a, op, b = m.groups()
        a, b = ev(a, local), ev(b, local) + (1 if op == "<=" else 0)
        body_start = i + 1
        end = None
        for v in range(a, b):
            l2 = dict(local); l2[var] = v
            try:
                end = run(body_start, l2, depth + 1)
            except Continue:
                end = skip(body_start)
        return end if end is not None else run(body_start, dict(local), depth + 1)  # 0 iteraciones: saltar igual
    m = re.match(r"if\s*\((.+)\)\s*$", s)
    if m:
        cond = bool(eval(clean_expr(m.group(1)), {"__builtins__": {}}, Env() | local))
        after_then = run(i + 1, local, depth + 1) if cond else skip(i + 1)
        j = after_then
        while j < len(lines) and lines[j].strip() == "": j += 1
        if j < len(lines) and lines[j].strip() == "else":
            return run(j + 1, local, depth + 1) if not cond else skip(j + 1)
        return after_then
    if s == "continue;": raise Continue()
    m = re.match(r"(?:::)?gLoadData\.AccessModel\((.*)\);\s*$", s)
    if m:
        access(m.group(1), local)
    return i + 1

def skip(i):
    """Salta una sentencia sin ejecutarla."""
    while lines[i].strip() == "": i += 1
    s = lines[i].strip()
    if s == "{":
        depth = 1; i += 1
        while depth:
            t = lines[i].strip()
            if t == "{": depth += 1
            elif t == "}": depth -= 1
            i += 1
        return i
    if re.match(r"(for|if)\s*\(", s): return skip(i + 1)
    return i + 1

def run_file(fn):
    global lines
    raw = function_body(*fn)
    out, stack = [], []
    for ln in raw:
        t = ln.strip()
        if t.startswith("#ifdef") or t.startswith("#if "): stack.append(True); continue
        if t.startswith("#ifndef"): stack.append(False); continue
        if t.startswith("#else"): stack[-1] = not stack[-1]; continue
        if t.startswith("#endif"): stack.pop(); continue
        if t.startswith("#"): continue
        if all(stack): out.append(re.sub(r"//.*$", "", ln).rstrip())
    lines = out
    i = next(k for k, l in enumerate(lines) if l.strip() == "{") + 1
    while i < len(lines):
        if lines[i].startswith("}"): break
        try:
            i = run(i, {})
        except Exception as e:
            print(f"{fn[1]}:{i}: {lines[i].strip()[:90]} -> {e}", file=sys.stderr)
            i += 1

for fn in FUNCTIONS:
    run_file(fn)

by_group = {}
for idx in models: by_group[idx // 512] = by_group.get(idx // 512, 0) + 1
print("modelos:", len(models), "por grupo:", dict(sorted(by_group.items())), file=sys.stderr)
json.dump({str(k): v for k, v in sorted(models.items())}, open(OUT, "w"), indent=0)
print("escrito", OUT, file=sys.stderr)
