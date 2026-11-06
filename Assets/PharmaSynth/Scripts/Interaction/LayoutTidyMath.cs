using UnityEngine;

/// Pure zoning grid for the center-table layout pass (place all the
/// equipment properly — facing, spacing, open space). The wide landscape deck
/// (y 0.91, x −1.85…1.40, z −3.94…−2.74; the player works the +z front edge)
/// is divided into four zones; every layout's items are re-seated onto the
/// deterministic slots below, in authored order:
///   • STATIONS  — ONE back row, 0.5 m pitch (verbs need elbow room; the
///     busiest module has 7 stations — exactly one full row)
///   • VESSELS   — center-front, where both hands can reach
///   • REAGENTS  — right-side grid (pourable bottles), 4 per column
///   • TOOLS     — left-side grid (rods, tongs, dishes…), 3 per column
/// The front strip z > −2.90 stays free for the builder's rack, spares and
/// match kits. Suite-pinned; the LayoutTidy menu writes these into the SOs.
public static class LayoutTidyMath
{
    public const float DeckY = 0.91f;
    public const float MinX = -1.85f, MaxX = 1.40f;
    public const float MinZ = -3.94f, MaxZ = -2.74f;
    public const float MinPairDistance = 0.14f;
    public const float MinStationDistance = 0.5f;
    public const int StationsPerRow = 7;

    /// Stations: one row along the back edge (x −1.80 → 1.20, 0.5 pitch).
    /// An 8th+ station (no module has one) overflows to a best-effort forward row.
    public static Vector3 StationPos(int i)
    {
        int row = i / StationsPerRow, col = i % StationsPerRow;
        return new Vector3(-1.80f + col * 0.5f, DeckY, -3.78f + row * 0.28f);
    }

    /// Vessels: center-front, 0.45 m apart.
    public static Vector3 VesselPos(int i)
        => new Vector3(-0.25f + i * 0.45f, DeckY, -3.18f);

    /// Pourable reagents: right-side grid, 4 per column, columns marching left.
    public static Vector3 ReagentPos(int i)
    {
        int col = i / 4, row = i % 4;
        return new Vector3(1.33f - col * 0.22f, DeckY, -3.52f + row * 0.19f);
    }

    /// Tools: left-side grid, 3 per column, columns marching right.
    public static Vector3 ToolPos(int i)
    {
        int col = i / 3, row = i % 3;
        return new Vector3(-1.70f + col * 0.24f, DeckY, -3.44f + row * 0.19f);
    }

    /// Rack + spares + match fixtures (builder-spawned, on the free front strip).
    public static Vector3 RackPos => new Vector3(0.95f, DeckY, -2.86f);
    public static Vector3 SparePos(int i) => new Vector3(-1.05f + i * 0.34f, DeckY, -2.86f);
    public static Vector3 MatchPos(int i) => new Vector3(-1.64f + i * 0.17f, DeckY, -2.88f);
    public static Vector3 StrikerPos => new Vector3(-1.30f, DeckY, -2.88f);

    public static bool OnDeck(Vector3 p)
        => p.x >= MinX - 0.01f && p.x <= MaxX + 0.01f && p.z >= MinZ - 0.01f && p.z <= MaxZ + 0.01f;
}
