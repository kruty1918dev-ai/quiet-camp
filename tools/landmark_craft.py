"""Metre-scale construction detail for the campaign's original low-poly landmarks.

This is a bake-time mesh builder. It changes construction, never level cameras,
puzzle occupancy, asset identity or live transforms.
"""
import math
import itertools
from build_ukrainian_roadmap_models import Mesh, PALETTE, length, sub, cross, dot


class CraftedLandmarkMesh(Mesh):
    def beam(self, a, b, width, color, closed=False, depth=None):
        span = length(sub(b, a))
        if color not in ('wood', 'wood_dark', 'wood_light') or span < 1.4 or width > .5:
            return super().beam(a, b, width, color, closed, depth)
        # A bowed old timber keeps its bearing ends fixed; the building never tilts wholesale.
        pieces = max(2, math.ceil(span / 1.1))
        sag = min(.045, span * .012)
        points = [tuple(a[k] + (b[k] - a[k]) * i / pieces - (math.sin(math.pi * i / pieces) * sag if k == 1 else 0) for k in range(3)) for i in range(pieces + 1)]
        for p, q in zip(points, points[1:]):
            super().beam(p, q, width, color, True, depth)

    def stone(self, center, size, color):
        """Closed chipped block: six inset faces, twelve bevels and eight corners."""
        # Stone-to-stone variation is broad and stable, with the original material hue.
        original = PALETTE[color] if isinstance(color, str) else color
        tone = .92 + .10 * (.5 + .5 * math.sin(center[0] * 9.7 + center[1] * 13.1 + center[2] * 5.3))
        color = sum(min(255, round(((original >> shift) & 255) * tone)) << shift for shift in (16, 8, 0))
        half = [v / 2 for v in size]
        radius = min(.038, min(size) * .13)
        corners = {}
        for signs in itertools.product((-1, 1), repeat=3):
            phase = sum((center[i] + signs[i] * half[i]) * (11.3, 7.1, 5.7)[i] for i in range(3))
            cut = radius * (.75 + .25 * math.sin(phase))
            for axis in range(3):
                corners[signs, axis] = tuple(center[i] + signs[i] * (half[i] - (0 if i == axis else cut)) for i in range(3))
        for axis in range(3):
            other = [i for i in range(3) if i != axis]
            for sign in (-1, 1):
                points = []
                for a, b in ((-1, -1), (-1, 1), (1, 1), (1, -1)):
                    s = [sign] * 3
                    s[other[0]], s[other[1]] = a, b
                    points.append(corners[tuple(s), axis])
                outward = [0, 0, 0]; outward[axis] = sign
                self.quad(*points, color, outward)
        for a, b in ((0, 1), (0, 2), (1, 2)):
            other = 3 - a - b
            for sa, sb in itertools.product((-1, 1), repeat=2):
                s = [1, 1, 1]; s[a], s[b] = sa, sb
                s[other] = -1; low = tuple(s)
                s[other] = 1; high = tuple(s)
                outward = [0, 0, 0]; outward[a], outward[b] = sa, sb
                self.quad(corners[low, a], corners[high, a], corners[high, b], corners[low, b], color, outward)
        for signs in itertools.product((-1, 1), repeat=3):
            points = [corners[signs, i] for i in range(3)]
            # A degenerate quad is unnecessary: use the base winding contract for this cap.
            if dot(cross(sub(points[1], points[0]), sub(points[2], points[0])), signs) < 0:
                points.reverse()
            self.tri(*points, color)

    def box(self, center, size, color):
        # Mortar joints follow actual courses, not a noise texture on a solid wall.
        if color in ('stone', 'plaster_worn') and size[1] > .55:
            axis = 0 if size[0] >= size[2] else 2
            rows = max(1, math.ceil(size[1] / .42))
            columns = max(1, math.ceil(size[axis] / .85))
            for row in range(rows):
                # Half-bond courses with closed end stones; no floating overhangs.
                cuts = [0] + [k / columns for k in range(1, columns)] + [1]
                if row % 2 and columns > 1:
                    cuts = [0] + [(k + .5) / columns for k in range(columns)] + [1]
                for start, end in zip(cuts, cuts[1:]):
                    p, s = list(center), list(size)
                    s[1] = size[1] / rows - .008
                    s[axis] = size[axis] * (end - start) - .008
                    p[1] += (row + .5) * size[1] / rows - size[1] / 2
                    p[axis] += ((start + end) / 2 - .5) * size[axis]
                    self.stone(p, s, color)
            return
        super().box(center, size, color)
