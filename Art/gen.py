#!/usr/bin/env python3
"""Identity Crisis art sources.

Builds the SVG sources for the mod's textures into ./svg.
Render them to PNG with render.js (sharp). Usage: python3 gen.py
"""
import math
import os

ROOT = os.path.dirname(os.path.abspath(__file__))
SVG_DIR = os.path.join(ROOT, "svg")
INK = "#17151c"

DEFS = """<defs>
<linearGradient id="icShell" x1="0" y1="0" x2="1" y2="0">
<stop offset="0" stop-color="#46435a"/><stop offset="0.35" stop-color="#726d86"/>
<stop offset="0.65" stop-color="#635e76"/><stop offset="1" stop-color="#36333f"/>
</linearGradient>
<linearGradient id="icPipe" x1="0" y1="0" x2="1" y2="0">
<stop offset="0" stop-color="#3a3846"/><stop offset="0.5" stop-color="#6c687a"/><stop offset="1" stop-color="#3a3846"/>
</linearGradient>
<radialGradient id="icGel" cx="0.5" cy="0.5" r="0.62">
<stop offset="0" stop-color="#dcb9f5"/><stop offset="0.55" stop-color="#9c62cb"/><stop offset="1" stop-color="#4b2170"/>
</radialGradient>
<linearGradient id="icBody" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#3a1d55" stop-opacity="0.75"/><stop offset="1" stop-color="#3a1d55" stop-opacity="0.15"/>
</linearGradient>
<linearGradient id="icSkin" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#e6d0b8"/><stop offset="1" stop-color="#c3a589"/>
</linearGradient>
<linearGradient id="icAlien" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#bb95e0"/><stop offset="1" stop-color="#7a4ea8"/>
</linearGradient>
<radialGradient id="icBg" cx="0.72" cy="0.5" r="0.75">
<stop offset="0" stop-color="#2f1b40"/><stop offset="1" stop-color="#0c0a10"/>
</radialGradient>
<clipPath id="icLeftHalf"><rect x="0" y="0" width="64" height="128"/></clipPath>
<clipPath id="icRightHalf"><rect x="64" y="0" width="64" height="128"/></clipPath>
<filter id="icBlur3" filterUnits="userSpaceOnUse" x="0" y="0" width="128" height="128"><feGaussianBlur stdDeviation="3"/></filter>
<filter id="icBlur20" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="20"/></filter>
</defs>"""


def assimilate():
    """Command icon: a head that is half human, half alien."""
    s = []
    a = s.append
    neck = '<path d="M50 96 V118 Q64 124 78 118 V96 Z" fill="url(#{fill})" stroke="' + INK + '" stroke-width="4" stroke-linejoin="round"/>'
    a('<g clip-path="url(#icLeftHalf)">' + neck.format(fill="icSkin") + '</g>')
    a('<g clip-path="url(#icRightHalf)">' + neck.format(fill="icAlien") + '</g>')
    a('<g clip-path="url(#icLeftHalf)">')
    a(f'<ellipse cx="31" cy="66" rx="7" ry="11" fill="url(#icSkin)" stroke="{INK}" stroke-width="4"/>')
    a(f'<ellipse cx="64" cy="62" rx="34" ry="42" fill="url(#icSkin)" stroke="{INK}" stroke-width="4"/>')
    a('<path d="M41 51 Q48 46 56 50" stroke="#5a4535" stroke-width="3" fill="none" stroke-linecap="round"/>')
    a('<ellipse cx="49" cy="60" rx="4.5" ry="3.6" fill="#2a211c"/>')
    a('<path d="M51 88 Q57 91 63 89" stroke="#7a5a48" stroke-width="3" fill="none" stroke-linecap="round"/>')
    a('</g>')
    a('<g clip-path="url(#icRightHalf)">')
    a(f'<path d="M64 10 C91 10 105 30 103 55 C101 81 85 101 64 104 Z" fill="url(#icAlien)" stroke="{INK}" '
      'stroke-width="4" stroke-linejoin="round"/>')
    a('<path d="M69 61 Q81 49 95 57 Q84 71 69 61 Z" fill="#150b1e"/>')
    a('<circle cx="87" cy="57" r="2.2" fill="#ffffff" opacity="0.85"/>')
    a('<path d="M71 92 Q77 94 83 90" stroke="#45265f" stroke-width="3" fill="none" stroke-linecap="round"/>')
    a('</g>')
    a('<path d="M64 6 V122" stroke="#d6a3ff" stroke-width="6" opacity="0.25"/>')
    a('<path d="M64 8 V120" stroke="#f7ecff" stroke-width="3" stroke-linecap="round"/>')
    for cx, cy, r in []:
        a(f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="#f1dcff"/>')
    return "".join(s)


def edit_setup():
    """Command icon: a DNA helix with a pencil."""
    s = []
    a = s.append
    n = 64
    turns = 1.25

    def dx_at(t):
        return 19 * math.sin(2 * math.pi * turns * t)

    pts_a = []
    pts_b = []
    for i in range(n + 1):
        t = i / n
        y = 12 + 100 * t
        pts_a.append((64 + dx_at(t), y))
        pts_b.append((64 - dx_at(t), y))

    def poly(pts):
        return "M" + " L".join(f"{x:.1f} {y:.1f}" for x, y in pts)

    a('<g transform="rotate(24 60 62)">')
    for k in range(1, 10):
        t = k / 10
        y = 12 + 100 * t
        dx = dx_at(t)
        if abs(dx) > 5:
            a(f'<path d="M{64 - abs(dx):.1f} {y:.1f} H{64 + abs(dx):.1f}" stroke="{INK}" stroke-width="7" stroke-linecap="round"/>')
            a(f'<path d="M{64 - abs(dx):.1f} {y:.1f} H{64 + abs(dx):.1f}" stroke="#d9d2e3" stroke-width="3" stroke-linecap="round"/>')
    for pts, color in [(pts_b, "#7fd3c6"), (pts_a, "#b583e6")]:
        a(f'<path d="{poly(pts)}" stroke="{INK}" stroke-width="11" fill="none" stroke-linecap="round" stroke-linejoin="round"/>')
        a(f'<path d="{poly(pts)}" stroke="{color}" stroke-width="6" fill="none" stroke-linecap="round" stroke-linejoin="round"/>')
    a('</g>')
    a('<g transform="translate(58 62) rotate(45) translate(0 -8)">')
    a(f'<path d="M0 8 L17 0 V16 Z" fill="#f0d9b5" stroke="{INK}" stroke-width="3.5" stroke-linejoin="round"/>')
    a('<path d="M0 8 L6 5.2 V10.8 Z" fill="#2d2a30"/>')
    a(f'<rect x="17" y="0" width="40" height="16" fill="#e9b949" stroke="{INK}" stroke-width="3.5"/>')
    a('<path d="M19 5.5 H55" stroke="#f6d77d" stroke-width="2.5"/>')
    a(f'<rect x="57" y="0" width="8" height="16" fill="#aab0b8" stroke="{INK}" stroke-width="3.5"/>')
    a(f'<rect x="65" y="0" width="11" height="16" rx="3" fill="#e58a8a" stroke="{INK}" stroke-width="3.5"/>')
    a('</g>')
    return "".join(s)


EAST = '<g transform="matrix(0 1 -1 0 256 0)">{}</g>'


def preview():
    """Mod list / Workshop preview, 640x360."""
    s = []
    a = s.append
    a('<rect width="640" height="360" fill="url(#icBg)"/>')
    for x in range(0, 641, 40):
        a(f'<path d="M{x} 0 V360" stroke="#ffffff" stroke-opacity="0.035"/>')
    for y in range(0, 361, 40):
        a(f'<path d="M0 {y} H640" stroke="#ffffff" stroke-opacity="0.035"/>')
    a('<ellipse cx="462" cy="182" rx="170" ry="80" fill="#8d4fc6" opacity="0.35" filter="url(#icBlur20)"/>')
    a('<g transform="translate(348 122) scale(1.0)">' + EAST.format(pod_north()) + '</g>')
    a('<text x="44" y="112" font-family="Arial" font-size="13" font-weight="bold" letter-spacing="3" fill="#a68fc4">RIMWORLD 1.6 MOD</text>')
    a('<text x="42" y="170" font-family="Arial" font-size="54" font-weight="bold" fill="#f5eefc">Identity</text>')
    a('<text x="42" y="226" font-family="Arial" font-size="54" font-weight="bold" fill="#f5eefc">Crisis</text>')
    a('<text x="44" y="268" font-family="Arial" font-size="18" fill="#cdbbe0">Rewrite the body.</text>')
    a('<text x="44" y="292" font-family="Arial" font-size="18" fill="#cdbbe0">Keep the scars.</text>')
    return "".join(s)


def svg(w, h, body):
    head = '<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %d %d">' % (w, h, w, h)
    return head + DEFS + body + "</svg>\n"



# v0.3: named, independently editable layers. Fixed canvas / rotation contract.
STATES = {
    "idle": ("#68517e", "#9d83b5", "M46 26 H82"),
    "active": ("#8958b2", "#c9a2ed", "M44 28 H52 L57 21 L63 32 L69 25 H84"),
    "no-power": ("#34333f", "#77727f", "M57 19 L51 27 H60 L56 33 M67 19 L76 33 M76 19 L67 33"),
    "completion": ("#637f76", "#9cdbb6", "M51 26 L60 32 L77 20"),
    "error": ("#765155", "#e6a183", "M57 19 L72 33 M72 19 L57 33"),
}


def pod_north(state="idle"):
    gel, light, symbol = STATES[state]
    # No blurry halo outside the silhouette: restrained rim and screen only.
    parts = [f'<g id="chassis" stroke="{INK}" stroke-linejoin="round">',
      '<rect x="3" y="74" width="11" height="108" rx="5.5" fill="url(#icPipe)" stroke-width="3"/>',
      '<rect x="114" y="74" width="11" height="108" rx="5.5" fill="url(#icPipe)" stroke-width="3"/>',
      '<rect x="10" y="6" width="108" height="244" rx="24" fill="url(#icShell)" stroke-width="4"/>',
      '<path d="M16 44 H112 M17 236 H34 M94 236 H111" stroke="#2a2833" stroke-width="3"/>',
      '<rect x="20" y="50" width="88" height="188" rx="18" fill="#221f2a" stroke-width="3"/>',
      '</g>',
      f'<g id="glass-{state}"><rect x="26" y="56" width="76" height="176" rx="14" fill="{gel}"/>',
      '<path d="M34 69 V219" stroke="#f4e8ff" stroke-width="4" stroke-linecap="round" opacity="0.16"/>',
      '<path d="M40 61 H88" stroke="#e1d3ec" stroke-width="2" opacity="0.24"/>',
      f'<rect x="26" y="56" width="76" height="176" rx="14" fill="none" stroke="{INK}" stroke-width="2.5"/></g>',
      '<g id="shell-details" fill="#292630">',
      '<rect x="46" y="241" width="36" height="4" rx="2"/>',
      '<path d="M15 86 H21 V104 H15 Z M107 86 H113 V104 H107 Z M15 176 H21 V194 H15 Z M107 176 H113 V194 H107 Z"/>',
      '</g>',
      f'<g id="indicators-{state}" stroke="{INK}">',
      '<rect x="38" y="15" width="52" height="21" rx="4" fill="#191c22" stroke-width="2.5"/>',
      f'<path d="{symbol}" fill="none" stroke="{light}" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>',
      f'<circle cx="25" cy="26" r="5" fill="{light}" stroke-width="2"/>',
      '<circle cx="103" cy="26" r="5" fill="#423e4b" stroke-width="2"/>',
      '</g>']
    if state == 'active':
        parts += ['<g id="activity-rim" fill="#d7bbef" opacity="0.72"><rect x="22" y="102" width="3" height="32" rx="1.5"/><rect x="103" y="154" width="3" height="32" rx="1.5"/></g>',
          '<g id="process-bubbles" fill="#e8d5f4" opacity="0.5"><circle cx="41" cy="194" r="2.5"/><circle cx="86" cy="124" r="2"/><circle cx="79" cy="207" r="2"/></g>']
    if state == 'completion':
        parts += ['<g id="completion-rim" stroke="#a4d8ba" stroke-width="3" fill="none"><path d="M22 75 V67 Q22 53 36 53 H46 M82 235 H92 Q106 235 106 221 V213"/></g>']
    if state == 'error':
        parts += ['<g id="fault-mark" stroke="#e6a183" stroke-width="3" stroke-linecap="round"><path d="M61 208 H67 M64 195 V202"/></g>']
    return ''.join(parts)


def oriented(state, direction):
    body = pod_north(state)
    if direction == 'south':
        body = '<g id="south-mirror" transform="translate(0 256) scale(1 -1)">' + body + '</g>'
    elif direction == 'east':
        body = EAST.format(body)
    w, h = (256, 128) if direction == 'east' else (128, 256)
    return svg(w, h, body)

FILES = {
    "IC_AssimilationPod_north.svg": lambda: svg(128, 256, pod_north()),
    "IC_AssimilationPod_south.svg": lambda: svg(128, 256, '<g transform="translate(0 256) scale(1 -1)">' + pod_north() + "</g>"),
    "IC_AssimilationPod_east.svg": lambda: svg(256, 128, EAST.format(pod_north())),
    "Assimilate.svg": lambda: svg(128, 128, '<g id="assimilation-command">' + assimilate() + '</g>'),
    "EditSetup.svg": lambda: svg(128, 128, '<g id="edit-setup-command">' + edit_setup() + '</g>'),
    "Preview.svg": lambda: svg(640, 360, preview()),
}


for _state in STATES:
    for _direction in ("north", "east", "south"):
        FILES[f"IC_AssimilationPod_{_state}_{_direction}.svg"] = lambda s=_state, d=_direction: oriented(s, d)


def main():
    os.makedirs(SVG_DIR, exist_ok=True)
    for name, build in FILES.items():
        with open(os.path.join(SVG_DIR, name), "w", encoding="utf-8") as f:
            f.write(build())
        print("wrote", name)


if __name__ == "__main__":
    main()
