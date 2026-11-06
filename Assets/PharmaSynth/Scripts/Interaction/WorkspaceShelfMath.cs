using UnityEngine;

/// Pure geometry for the center-table overhead shelf. One row of
/// flush platform tiles sits on the gantry rails (front z=-3.15, back z=-3.50, tops
/// y≈1.55). A SECOND, lower row (plank centres
/// y≈1.200) for the apparatus kits — this math describes BOTH rows so the
/// builder can rebuild them cleanly and the kit layout can target either.
/// Values match the hand-placed planks (row pitch, z centre, depth). Kept plain so
/// the suite pins coverage + heights.
public static class WorkspaceShelfMath
{
    public const int Rows = 2;
    public const float Thickness = 0.03f;
    public const float ZCenter = -3.32f;        // between the two rails
    public const float Depth = 0.34f;           // bridges z -3.49 .. -3.15
    public const float XMin = -1.40f, XMax = 1.40f;
    public const float Gap = 0.02f;             // hairline seam between tiles

    /// Row 0 = the original top gantry row; row 1 = the hand-added lower row.
    public static float RowCenterY(int row) => row == 0 ? 1.548f : 1.200f;

    /// Legacy single-row aliases (row 0) — existing callers/tests keep working.
    public const float TileCenterY = 1.548f;
    public static float TopY => TopYOf(0);

    /// Top surface where equipment rests.
    public static float TopYOf(int row) => RowCenterY(row) + Thickness * 0.5f;

    /// Headroom above the LOWER row before the top row's underside: tall items
    /// (retort stand, burner) must go on row 0.
    public static float LowerRowHeadroom => RowCenterY(0) - Thickness * 0.5f - TopYOf(1);

    static float CellWidth(int count) => (XMax - XMin) / Mathf.Max(1, count);

    /// Centre of tile i of `count`, evenly dividing [XMin, XMax].
    public static Vector3 TileCenter(int i, int count, int row = 0)
        => new Vector3(XMin + (i + 0.5f) * CellWidth(count), RowCenterY(row), ZCenter);

    /// Tile box size (cell width minus the seam gap).
    public static Vector3 TileSize(int count)
        => new Vector3(Mathf.Max(0.05f, CellWidth(count) - Gap), Thickness, Depth);

    /// A subtle back-lip strip so tall items don't roll off the far edge.
    public static Vector3 LipCenter(int row = 0)
        => new Vector3(0f, RowCenterY(row) + 0.02f + Thickness * 0.5f, ZCenter - Depth * 0.5f + 0.01f);
    public static Vector3 LipSize => new Vector3(XMax - XMin, 0.05f, 0.015f);
}
