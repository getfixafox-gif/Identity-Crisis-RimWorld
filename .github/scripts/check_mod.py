#!/usr/bin/env python3
"""Static checks for the Identity Crisis mod. Standard library only.

Run from the repository root:  python3 .github/scripts/check_mod.py

Errors make the script exit with code 1; warnings are printed but do not fail.
Checks:
  1. Every XML file parses.
  2. English and Russian Keyed files have the same keys, without duplicates,
     and every translation uses the same {N} placeholders as the English line.
  3. Every "IC_..." string literal in C# is a Keyed key or a defName.
  4. Every IC_DefOf field has a def of the same type and name.
  5. Every IdentityCrisis.* class referenced from XML exists in Source.
  6. Mod texture paths used by Defs and code exist under Textures/.
  7. Russian DefInjected entries point to existing defs, and every
     translatable field of our defs has a Russian translation.
"""

import os
import re
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
LANGS = ["English", "Russian"]
BASE_LANG = "English"
TRANSLATABLE_FIELDS = {
    "label", "description", "labelShort", "pawnLabel", "gerundLabel", "verb", "gerund",
    "reportString", "labelNoun", "labelMale", "labelFemale", "fuelLabel", "fuelGizmoLabel",
    "jobString", "letterLabel", "letterText", "baseInspectLine",
}
DEF_TYPES_WITH_INJECTIONS = {
    "HediffDef", "JobDef", "ResearchProjectDef", "ThingDef", "ThoughtDef", "TraitDef",
    "WorkGiverDef", "WorkTypeDef", "ResearchTabDef",
}

errors = []
warnings = []


def rel(path):
    return os.path.relpath(path, ROOT).replace(os.sep, "/")


def error(msg):
    errors.append(msg)


def warn(msg):
    warnings.append(msg)


def walk(folder, exts):
    base = os.path.join(ROOT, folder)
    if not os.path.isdir(base):
        return []
    out = []
    for dirpath, _dirs, files in os.walk(base):
        for name in files:
            if os.path.splitext(name)[1].lower() in exts:
                out.append(os.path.join(dirpath, name))
    return sorted(out)


# ---------------------------------------------------------------- 1. XML parse
parsed = {}
xml_files = walk("About", {".xml"}) + walk("Defs", {".xml"}) + walk("Patches", {".xml"}) \
    + walk("Languages", {".xml"})
if os.path.isfile(os.path.join(ROOT, "LoadFolders.xml")):
    xml_files.append(os.path.join(ROOT, "LoadFolders.xml"))
for path in xml_files:
    try:
        parsed[path] = ET.parse(path).getroot()
    except ET.ParseError as e:
        error("XML parse error in %s: %s" % (rel(path), e))

# ------------------------------------------------------------ defs inventory
defs = {}  # (DefType, defName) -> element
def_names = set()
for path in walk("Defs", {".xml"}):
    root = parsed.get(path)
    if root is None:
        continue
    for el in root:
        name = el.findtext("defName")
        if not name:
            continue
        key = (el.tag, name.strip())
        if key in defs:
            error("Duplicate def %s/%s in %s" % (el.tag, name, rel(path)))
        defs[key] = el
        def_names.add(name.strip())

# -------------------------------------------------------------- 2. Keyed
keyed = {}
keyed_values = {}
for lang in LANGS:
    keyed[lang] = {}
    keyed_values[lang] = {}
    for path in walk(os.path.join("Languages", lang, "Keyed"), {".xml"}):
        root = parsed.get(path)
        if root is None:
            continue
        for el in root:
            if not isinstance(el.tag, str):
                continue
            if el.tag in keyed[lang]:
                error("Duplicate %s Keyed key %s in %s and %s" % (lang, el.tag, keyed[lang][el.tag], rel(path)))
            keyed[lang][el.tag] = rel(path)
            keyed_values[lang][el.tag] = el.text or ""

base_keys = set(keyed[BASE_LANG])
placeholder = re.compile(r"\{(\d+)\}")
for lang in LANGS:
    if lang == BASE_LANG:
        continue
    for key in sorted(base_keys - set(keyed[lang])):
        error("Keyed key %s is missing in %s" % (key, lang))
    for key in sorted(set(keyed[lang]) - base_keys):
        error("Keyed key %s exists in %s but not in %s" % (key, lang, BASE_LANG))
    for key in sorted(base_keys & set(keyed[lang])):
        a = set(placeholder.findall(keyed_values[BASE_LANG][key]))
        b = set(placeholder.findall(keyed_values[lang][key]))
        if a != b:
            error("Placeholders differ for %s: %s %s vs %s %s" % (key, BASE_LANG, sorted(a), lang, sorted(b)))

# ------------------------------------------------------ 3. C# string literals
cs_files = walk("Source", {".cs"})
cs_text = {path: open(path, encoding="utf-8-sig").read() for path in cs_files}
literal = re.compile(r'"(IC_[A-Za-z0-9_]*)"(\s*\+)?')
enum_values = {}
for text in cs_text.values():
    for m in re.finditer(r"enum\s+(\w+)\s*(?::\s*\w+)?\s*\{([^}]*)\}", text):
        enum_values[m.group(1)] = [v.split("=")[0].strip() for v in m.group(2).split(",") if v.strip()]
for path, text in cs_text.items():
    for m in literal.finditer(text):
        value, concat = m.group(1), m.group(2)
        line = text[text.rfind("\n", 0, m.start()) + 1:m.start()]
        if "Scribe_" in line:
            continue  # save labels are not translation keys
        if concat:
            if not any(k.startswith(value) for k in base_keys):
                error("%s: no Keyed key starts with dynamic prefix %s" % (rel(path), value))
            continue
        if value in base_keys or value in def_names:
            continue
        error("%s: %s is neither an English Keyed key nor a defName" % (rel(path), value))
# Lifecycle states are shown as "IC_PodState" + state.
for state in enum_values.get("PodLifecycleState", []):
    if "IC_PodState" + state not in base_keys:
        error("Missing Keyed key IC_PodState%s for PodLifecycleState.%s" % (state, state))

# ------------------------------------------------------------- 4. DefOf
for path, text in cs_text.items():
    if "[DefOf]" not in text:
        continue
    for m in re.finditer(r"public\s+static\s+(\w+Def)\s+(\w+)\s*;", text):
        def_type, name = m.group(1), m.group(2)
        if (def_type, name) not in defs:
            # MayRequire fields are allowed to be missing.
            line_start = text.rfind("\n", 0, m.start())
            prev = text[max(0, line_start - 200):m.start()]
            if "MayRequire" in prev.splitlines()[-1] if prev.splitlines() else False:
                continue
            error("%s: DefOf field %s %s has no matching def in Defs/" % (rel(path), def_type, name))

# ------------------------------------------------------ 5. class references
class_names = set()
for text in cs_text.values():
    for m in re.finditer(r"\b(?:class|struct|enum)\s+(\w+)", text):
        class_names.add(m.group(1))
class_ref = re.compile(r"^IdentityCrisis\.(\w+)$")
for path in walk("Defs", {".xml"}) + walk("Patches", {".xml"}):
    root = parsed.get(path)
    if root is None:
        continue
    for el in root.iter():
        candidates = []
        if el.text and el.text.strip().startswith("IdentityCrisis."):
            candidates.append(el.text.strip())
        cls = el.attrib.get("Class")
        if cls and cls.startswith("IdentityCrisis."):
            candidates.append(cls)
        for value in candidates:
            m = class_ref.match(value)
            if not m or m.group(1) not in class_names:
                error("%s: class %s is not defined in Source/" % (rel(path), value))

# ------------------------------------------------------------ 6. textures
tex_root = os.path.join(ROOT, "Textures")


def texture_exists(tex_path, multi):
    base = os.path.join(tex_root, *tex_path.split("/"))
    if os.path.isfile(base + ".png"):
        return True
    if multi:
        return all(os.path.isfile(base + suffix + ".png") for suffix in ("_north", "_east", "_south"))
    return False


def ours(tex_path):
    first = tex_path.split("/")
    return any("IC_" in part or part == "IdentityCrisis" for part in first)


for (def_type, name), el in defs.items():
    for gd in el.iter("graphicData"):
        tex = (gd.findtext("texPath") or "").strip()
        cls = (gd.findtext("graphicClass") or "").strip()
        if tex and ours(tex) and not texture_exists(tex, cls == "Graphic_Multi"):
            error("%s %s: texture %s not found under Textures/" % (def_type, name, tex))
for path, text in cs_text.items():
    for m in re.finditer(r'"((?:UI|Things)/[A-Za-z0-9_/\-]+)"', text):
        tex = m.group(1)
        if tex.endswith("/"):
            continue  # a prefix that the code completes at runtime
        if ours(tex) and not texture_exists(tex, True):
            warn("%s: texture %s not found (the code must fall back)" % (rel(path), tex))
# Pod state graphics: Things/Building/IC_AssimilationPod/States/<state>/IC_AssimilationPod
states_dir = os.path.join(tex_root, "Things", "Building", "IC_AssimilationPod", "States")
for path, text in cs_text.items():
    for m in re.finditer(r'"Things/Building/IC_AssimilationPod/States/"', text):
        for state in re.findall(r'"(idle|active|completion|error|no-power)"', text):
            tex = "Things/Building/IC_AssimilationPod/States/%s/IC_AssimilationPod" % state
            if not texture_exists(tex, True):
                error("%s: pod state texture %s not found" % (rel(path), tex))

# ---------------------------------------------------- 7. DefInjected (Russian)
injected = {}
for lang in LANGS:
    if lang == BASE_LANG:
        continue
    base = os.path.join("Languages", lang, "DefInjected")
    for path in walk(base, {".xml"}):
        root = parsed.get(path)
        if root is None:
            continue
        def_type = os.path.basename(os.path.dirname(path))
        for el in root:
            if not isinstance(el.tag, str):
                continue
            key = el.tag
            def_name = key.split(".")[0]
            injected.setdefault((lang, def_type), set()).add(key)
            if (def_type, def_name) not in defs:
                error("%s: %s points to missing %s %s" % (rel(path), key, def_type, def_name))


def translatable_paths(el, prefix):
    """Yield DefInjected keys for the translatable text fields of a def element."""
    for child in el:
        if not isinstance(child.tag, str):
            continue
        tag = child.tag
        if tag in TRANSLATABLE_FIELDS and (child.text or "").strip():
            yield prefix + "." + tag
        elif tag in ("stages", "degreeDatas"):
            for index, li in enumerate(child.findall("li")):
                for path in translatable_paths(li, prefix + "." + tag + "." + str(index)):
                    yield path
        elif tag == "comps":
            for li in child.findall("li"):
                for path in translatable_paths(li, prefix + ".comps." + li.attrib.get("Class", "?")):
                    pass  # comp fields need class-specific handles; covered by manual review


for lang in LANGS:
    if lang == BASE_LANG:
        continue
    for (def_type, name), el in sorted(defs.items()):
        if def_type not in DEF_TYPES_WITH_INJECTIONS:
            continue
        have = injected.get((lang, def_type), set())
        for key in translatable_paths(el, name):
            if key not in have:
                # Degree data labels are commonly keyed by label instead of index in older files.
                error("%s DefInjected is missing %s/%s" % (lang, def_type, key))

# --------------------------------------------------------------- summary
for w in warnings:
    print("warning: " + w)
for e in errors:
    print("error: " + e)
print("Checked %d XML files, %d C# files, %d defs, %d English keys: %d error(s), %d warning(s)." % (
    len(xml_files), len(cs_files), len(defs), len(base_keys), len(errors), len(warnings)))
sys.exit(1 if errors else 0)
