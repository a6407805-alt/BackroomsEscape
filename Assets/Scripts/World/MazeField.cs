using UnityEngine;

namespace Backrooms
{
    public enum PickupKind { None, Battery, AlmondWater }

    /// <summary>
    /// The infinite layout as pure functions of (seed, cell): walls, open halls, pillars, lights, dark zones, exits and
    /// pickups. Nothing is stored, so the world is endless and the same every time you come back to a spot.
    /// Cell (x, z) covers world [x*S, (x+1)*S] x [z*S, (z+1)*S]. Each cell owns its East and North wall.
    /// </summary>
    public class MazeField
    {
        public readonly int Seed;
        public readonly float S;
        public readonly int ChunkCells;
        public readonly LevelTheme Theme;
        public Vector2Int StartCell = Vector2Int.zero;
        /// <summary>Cells kept free of walls (where drum pieces lie).</summary>
        public readonly System.Collections.Generic.HashSet<Vector2Int> OpenCells = new System.Collections.Generic.HashSet<Vector2Int>();
        private bool Open(int x, int z) => OpenCells.Contains(new Vector2Int(x, z));

        private const int SaltWallE = 1, SaltWallN = 2, SaltRoom = 3, SaltPillar = 4, SaltLight = 5, SaltBroken = 6, SaltFlicker = 7,
                          SaltDark = 8, SaltExitX = 9, SaltExitZ = 10, SaltExit = 11, SaltPickup = 12, SaltPickupKind = 13, SaltExitDir = 14;

        public MazeField(int seed, float cellSize, int chunkCells, LevelTheme theme)
        {
            Seed = seed; S = cellSize; ChunkCells = chunkCells; Theme = theme;
        }

        // ------------------------------------------------------------------ cells <-> world
        public Vector2Int CellAt(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / S), Mathf.FloorToInt(p.z / S));
        public Vector3 CellCenter(Vector2Int c) => new Vector3((c.x + 0.5f) * S, 0f, (c.y + 0.5f) * S);
        public Vector2Int ChunkOf(Vector2Int cell) => new Vector2Int(Mathf.FloorToInt(cell.x / (float)ChunkCells), Mathf.FloorToInt(cell.y / (float)ChunkCells));

        private bool NearStart(int x, int z) => Mathf.Abs(x - StartCell.x) <= 1 && Mathf.Abs(z - StartCell.y) <= 1;

        // ------------------------------------------------------------------ halls (open rooms in 3x3 blocks)
        public bool InHall(int x, int z)
        {
            int rx = Mathf.FloorToInt(x / 3f), rz = Mathf.FloorToInt(z / 3f);
            return Hash.F(Seed, rx, rz, SaltRoom) < Theme.roomChance;
        }

        // ------------------------------------------------------------------ walls
        private bool RawE(int x, int z)
        {
            if (NearStart(x, z) && NearStart(x + 1, z)) return false;
            if (InHall(x, z) && InHall(x + 1, z)) return false;
            return Hash.F(Seed, x, z, SaltWallE) < Theme.wallChance;
        }

        private bool RawN(int x, int z)
        {
            if (NearStart(x, z) && NearStart(x, z + 1)) return false;
            if (InHall(x, z) && InHall(x, z + 1)) return false;
            return Hash.F(Seed, x, z, SaltWallN) < Theme.wallChance;
        }

        /// <summary>A cell walled on all four sides gets its cheapest wall knocked out, so no pocket is sealed.</summary>
        private bool Sealed(int x, int z) => RawE(x, z) && RawN(x, z) && RawE(x - 1, z) && RawN(x, z - 1);

        /// <summary>Which of a sealed cell's walls is removed: 0=E 1=N 2=W 3=S.</summary>
        private int OpenSide(int x, int z) => (int)(Hash.U(Seed, x, z, 99) % 4);

        public bool WallE(int x, int z)
        {
            if (!RawE(x, z)) return false;
            if (IsExitCell(x, z) || IsExitCell(x + 1, z) || Open(x, z) || Open(x + 1, z)) return false;
            if (Sealed(x, z) && OpenSide(x, z) == 0) return false;
            if (Sealed(x + 1, z) && OpenSide(x + 1, z) == 2) return false;
            return true;
        }

        public bool WallN(int x, int z)
        {
            if (!RawN(x, z)) return false;
            if (IsExitCell(x, z) || IsExitCell(x, z + 1) || Open(x, z) || Open(x, z + 1)) return false;
            if (Sealed(x, z) && OpenSide(x, z) == 1) return false;
            if (Sealed(x, z + 1) && OpenSide(x, z + 1) == 3) return false;
            return true;
        }

        /// <summary>Can you step from cell a to the neighbouring cell b?</summary>
        public bool Passable(Vector2Int a, Vector2Int b)
        {
            int dx = b.x - a.x, dz = b.y - a.y;
            if (dx == 1 && dz == 0) return !WallE(a.x, a.y);
            if (dx == -1 && dz == 0) return !WallE(b.x, b.y);
            if (dz == 1 && dx == 0) return !WallN(a.x, a.y);
            if (dz == -1 && dx == 0) return !WallN(b.x, b.y);
            return false;
        }

        /// <summary>Pillar at the grid corner (x, z) (the corner at the cell's min x/min z).</summary>
        public bool PillarAt(int x, int z)
        {
            if (NearStart(x, z) || NearStart(x - 1, z - 1)) return false;
            return InHall(x, z) && Hash.F(Seed, x, z, SaltPillar) < Theme.pillarChance * 3f;
        }

        // ------------------------------------------------------------------ lights
        public bool DarkChunk(Vector2Int chunk)
        {
            if (chunk == ChunkOf(StartCell)) return false;
            return Hash.F(Seed, chunk.x, chunk.y, SaltDark) < Theme.darkZoneChance;
        }

        public bool HasLight(int x, int z)
        {
            int e = Mathf.Max(1, Theme.lightEveryCells);
            return ((x % e) + e) % e == 0 && ((z % e) + e) % e == 0;
        }

        public bool LightBroken(int x, int z) => !NearStart(x, z) && Hash.F(Seed, x, z, SaltBroken) < Theme.lightBrokenChance;
        public bool LightFlickers(int x, int z) => Hash.F(Seed, x, z, SaltFlicker) < Theme.lightFlickerChance;

        // ------------------------------------------------------------------ exits: at most one per region
        public bool RegionExit(int rx, int rz, out Vector2Int cell)
        {
            int R = Mathf.Max(4, Theme.exitRegionCells);
            int ox = (int)(Hash.U(Seed, rx, rz, SaltExitX) % (uint)(R - 2)) + 1;
            int oz = (int)(Hash.U(Seed, rx, rz, SaltExitZ) % (uint)(R - 2)) + 1;
            cell = new Vector2Int(rx * R + ox, rz * R + oz);
            if (Hash.F(Seed, rx, rz, SaltExit) >= Theme.exitChancePerRegion) return false;
            int dx = cell.x - StartCell.x, dz = cell.y - StartCell.y;
            return dx * dx + dz * dz >= Theme.exitMinDistanceCells * Theme.exitMinDistanceCells;
        }

        public bool IsExitCell(int x, int z)
        {
            int R = Mathf.Max(4, Theme.exitRegionCells);
            int rx = Mathf.FloorToInt(x / (float)R), rz = Mathf.FloorToInt(z / (float)R);
            return RegionExit(rx, rz, out var c) && c.x == x && c.y == z;
        }

        /// <summary>Facing of the exit door in a cell, in 90 degree steps.</summary>
        public int ExitYawSteps(int x, int z) => (int)(Hash.U(Seed, x, z, SaltExitDir) % 4);

        /// <summary>Nearest exit to a cell, searching nearby regions.</summary>
        public bool NearestExit(Vector2Int from, out Vector2Int exit)
        {
            int R = Mathf.Max(4, Theme.exitRegionCells);
            int rx0 = Mathf.FloorToInt(from.x / (float)R), rz0 = Mathf.FloorToInt(from.y / (float)R);
            float best = float.MaxValue; exit = default;
            for (int r = 0; r <= 4; r++)
            {
                for (int rx = rx0 - r; rx <= rx0 + r; rx++)
                    for (int rz = rz0 - r; rz <= rz0 + r; rz++)
                    {
                        if (Mathf.Max(Mathf.Abs(rx - rx0), Mathf.Abs(rz - rz0)) != r) continue;
                        if (!RegionExit(rx, rz, out var c)) continue;
                        float d = (c - from).sqrMagnitude;
                        if (d < best) { best = d; exit = c; }
                    }
                if (best < float.MaxValue && r >= 1) return true;
            }
            return best < float.MaxValue;
        }

        // ------------------------------------------------------------------ pickups
        public PickupKind PickupAt(int x, int z)
        {
            if (NearStart(x, z) || IsExitCell(x, z) || Open(x, z)) return PickupKind.None;
            if (Hash.F(Seed, x, z, SaltPickup) >= Theme.pickupChance) return PickupKind.None;
            return Hash.F(Seed, x, z, SaltPickupKind) < 0.55f ? PickupKind.Battery : PickupKind.AlmondWater;
        }
    }
}
