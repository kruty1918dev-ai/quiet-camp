"""Construction geometry contracts: closed stones, physical bounds and pinned timbers."""
import collections
import math
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from landmark_craft import CraftedLandmarkMesh


class LandmarkCraft(unittest.TestCase):
    def test_chipped_stone_is_closed_and_has_outward_normals(self):
        m = CraftedLandmarkMesh('stone', 200, '')
        m.stone((0, 0, 0), (1, .4, .3), 'stone')
        edges = collections.Counter()
        for a, b, c, normal, _ in m.tris:
            centre = tuple((a[i] + b[i] + c[i]) / 3 for i in range(3))
            self.assertGreater(sum(centre[i] * normal[i] for i in range(3)), 0)
            self.assertAlmostEqual(sum(v * v for v in normal), 1)
            for p, q in ((a, b), (b, c), (c, a)):
                edges[tuple(sorted((p, q)))] += 1
        self.assertTrue(all(count == 2 for count in edges.values()), 'An open or nonmanifold stone edge')
        self.assertEqual(((-.5, -.2, -.15), (.5, .2, .15)), tuple(tuple(v) for v in m.bounds()))

    def test_courses_stay_inside_the_original_foundation(self):
        m = CraftedLandmarkMesh('wall', 5000, '')
        m.box((0, 1, 0), (3, 2, .3), 'plaster_worn')
        low, high = m.bounds()
        for i, extent in enumerate((1.5, 1, .15)):
            self.assertGreaterEqual(low[i], (0, 1, 0)[i] - extent - 1e-7)
            self.assertLessEqual(high[i], (0, 1, 0)[i] + extent + 1e-7)
        self.assertGreater(len(m.tris), 44)

    def test_timber_bow_keeps_its_bearing_ends(self):
        m = CraftedLandmarkMesh('timber', 200, '')
        m.beam((0, 2, 0), (4, 2, 0), .2, 'wood', True)
        # End-ring centres stay at the specified supports, while the middle has a sag.
        for x, cap in ((0, m.tris[8:10]), (4, m.tris[-2:])):
            points = set(p for tri in cap for p in tri[:3])
            self.assertEqual(4, len(points))
            self.assertAlmostEqual(x, sum(p[0] for p in points) / 4)
            self.assertAlmostEqual(2, sum(p[1] for p in points) / 4)
        self.assertLess(min(p[1] for t in m.tris for p in t[:3]), 1.89)
        self.assertTrue(all(math.isfinite(v) for t in m.tris for p in t[:3] for v in p))


if __name__ == '__main__':
    unittest.main()
