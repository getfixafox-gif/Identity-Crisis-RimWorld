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


def pod_north():
    """Assimilation pod facing north, 128x256 (1x2 cells). Control panel at the far end."""
    s = []
    a = s.append
    a(f'<rect x="3" y="74" width="11" height="108" rx="5.5" fill="url(#icPipe)" stroke="{INK}" stroke-width="3"/>')
    a(f'<rect x="114" y="74" width="11" height="108" rx="5.5" fill="url(#icPipe)" stroke="{INK}" stroke-width="3"/>')
    a(f'<rect x="10" y="6" width="108" height="244" rx="24" fill="url(#icShell)" stroke="{INK}" stroke-width="4"/>')
    a('<path d="M16 44 H112" stroke="#2a2833" stroke-width="3" stroke-linecap="round"/>')
    a(f'<rect x="20" y="50" width="88" height="188" rx="18" fill="#221f2a" stroke="{INK}" stroke-width="3"/>')
    a('<rect x="26" y="56" width="76" height="176" rx="14" fill="url(#icGel)"/>')
    a('<g fill="url(#icBody)">'
      '<circle cx="64" cy="86" r="12"/>'
      '<path d="M50 102 Q64 96 78 102 L82 152 Q80 160 74 162 L72 214 Q70 220 66 220 L64 172 '
      'L62 220 Q58 220 56 214 L54 162 Q48 160 46 152 Z"/>'
      '</g>')
    for cx, cy, r in [(38, 204, 3), (90, 184, 2.5), (40, 142, 2), (88, 120, 3),
                      (72, 210, 2), (36, 100, 2), (92, 214, 2), (60, 66, 2)]:
        a(f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="#f3e4ff" opacity="0.75"/>')
    a('<path d="M34 68 Q30 144 34 220" stroke="#ffffff" stroke-width="5" stroke-linecap="round" fill="none" opacity="0.22"/>')
    a(f'<rect x="26" y="56" width="76" height="176" rx="14" fill="none" stroke="{INK}" stroke-width="2.5"/>')
    a(f'<rect x="38" y="15" width="52" height="21" rx="4" fill="#13201a" stroke="{INK}" stroke-width="2.5"/>')
    a('<path d="M43 29 H51 L55 21 L60 33 L64 25 H85" stroke="#8ef0b0" stroke-width="2" fill="none" '
      'stroke-linejoin="round" stroke-linecap="round"/>')
    a(f'<circle cx="25" cy="26" r="5" fill="#c58af0" stroke="{INK}" stroke-width="2"/>')
    a(f'<circle cx="103" cy="26" r="5" fill="#f0b24a" stroke="{INK}" stroke-width="2"/>')
    a('<rect x="46" y="241" width="36" height="4" rx="2" fill="#24212c"/>')
    return "".join(s)


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
    a('<path d="M64 6 V122" stroke="#d6a3ff" stroke-width="9" opacity="0.6" filter="url(#icBlur3)"/>')
    a('<path d="M64 8 V120" stroke="#f7ecff" stroke-width="3" stroke-linecap="round"/>')
    for cx, cy, r in [(57, 28, 2), (72, 42, 2.4), (55, 76, 1.8), (73, 100, 2), (58, 112, 1.6)]:
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
    a('<g transform="translate(302 102) scale(1.25)">' + EAST.format(pod_north()) + '</g>')
    a('<text x="44" y="112" font-family="Arial" font-size="13" font-weight="bold" letter-spacing="3" fill="#a68fc4">RIMWORLD 1.6 MOD</text>')
    a('<text x="42" y="170" font-family="Arial" font-size="54" font-weight="bold" fill="#f5eefc">Identity</text>')
    a('<text x="42" y="226" font-family="Arial" font-size="54" font-weight="bold" fill="#f5eefc">Crisis</text>')
    a('<text x="44" y="268" font-family="Arial" font-size="18" fill="#cdbbe0">Rewrite the body.</text>')
    a('<text x="44" y="292" font-family="Arial" font-size="18" fill="#cdbbe0">Keep the scars.</text>')
    return "".join(s)


def svg(w, h, body):
    head = '<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %d %d">' % (w, h, w, h)
    return head + DEFS + body + "</svg>\n"


FILES = {
    "IC_AssimilationPod_north.svg": lambda: svg(128, 256, pod_north()),
    "IC_AssimilationPod_south.svg": lambda: svg(128, 256, '<g transform="translate(0 256) scale(1 -1)">' + pod_north() + "</g>"),
    "IC_AssimilationPod_east.svg": lambda: svg(256, 128, EAST.format(pod_north())),
    "Assimilate.svg": lambda: svg(128, 128, assimilate()),
    "EditSetup.svg": lambda: svg(128, 128, edit_setup()),
    "Preview.svg": lambda: svg(640, 360, preview()),
}


def main():
    os.makedirs(SVG_DIR, exist_ok=True)
    for name, build in FILES.items():
        with open(os.path.join(SVG_DIR, name), "w", encoding="utf-8") as f:
            f.write(build())
        print("wrote", name)


if __name__ == "__main__":
    main()
