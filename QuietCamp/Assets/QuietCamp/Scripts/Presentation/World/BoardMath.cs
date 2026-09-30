using QuietCamp.Domain;
using UnityEngine;
namespace QuietCamp.Presentation.World
{
    /// <summary>Exact board geometry from the scene contract: XZ plane, +Y up,
    /// 1 cell = 1 m, board centred on origin.</summary>
    public static class BoardMath
    {
        public const float CellSize = 1f;
        public const float BoardTopY = 0f;
        public const float BaseCenterY = -0.15f;
        public const float BaseThickness = 0.30f;
        public const float Overhang = 0.10f;
        public const float GridLineWidth = 0.018f;
        public const float GridY = 0.006f;
        public const float OverlayY = 0.03f;
        public const float TentY = 0.02f;
        public const float SelectedLift = 0.08f;
        public const float DragThresholdDp = 8f;
        public const float GrabHysteresis = 0.08f;

        public static Vector3 CellCenter(LevelData l, int x, int z)
            => new Vector3(x + 0.5f - l.width / 2f, 0f, z + 0.5f - l.height / 2f);

        public static Vector3 TentCenter(LevelData l, int x, int z)
            => new Vector3(x + 1f - l.width / 2f, TentY, z + 1f - l.height / 2f);

        public static Vector3 CellCenterWorld(LevelData l, Cell c)
            => CellCenter(l, c.X, c.Z);

        /// <summary>Board cell containing a world XZ point; intentionally unclamped.</summary>
        public static Cell CellOf(LevelData l, Vector3 point)
            => new Cell(
                Mathf.FloorToInt(point.x + l.width / 2f),
                Mathf.FloorToInt(point.z + l.height / 2f));

        public static int TentYaw(int rotation) => 90 * rotation;

        /// <summary>Door-marker world position for a placement at cell resolution.</summary>
        public static Vector3 DoorWorld(LevelData l, Placement p)
        {
            var door = RuleEvaluator.Door(p);
            return CellCenter(l, door.X, door.Z);
        }
    }
}
