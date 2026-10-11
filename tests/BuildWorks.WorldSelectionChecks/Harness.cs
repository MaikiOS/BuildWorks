using System;
using System.Collections.Generic;
using System.Linq;

// Execute the actual area-selection method with instrumented host substitutes, without Unity.
public sealed class WorldSelectionChecks
{
    private readonly List<Piece> nearbyPieces = new List<Piece>();
    private readonly List<Piece> selected = new List<Piece>();
    private readonly List<Piece> attempts = new List<Piece>();
    private int screenCalls, budget = int.MaxValue;
    private bool removing;

    // PRODUCTION_METHOD

    private bool IsBlueprintPieceSelectable(Piece piece) { return piece.Selectable; }
    private bool TryPieceScreenPoint(Camera camera, Piece piece, out Vector2 screen)
    {
        ++screenCalls;
        screen = piece.Screen;
        return piece.Projectable;
    }
    private bool TryGetPlacedPieceAt(Camera camera, Vector2 screen, out Piece hit, out int unused)
    {
        unused = 0;
        return camera.Hits.TryGetValue(screen, out hit);
    }
    private bool SetBlueprintPieceSelected(Piece piece, bool remove, bool toggle)
    {
        Require(remove == removing && !toggle, "Area selection changed remove/toggle semantics.");
        attempts.Add(piece);
        if (remove) { selected.Remove(piece); return true; }
        if (selected.Contains(piece)) return true;
        if (selected.Count >= budget) return false;
        selected.Add(piece);
        return true;
    }

    public static string Run()
    {
        var camera = new Camera();
        var near = new Piece(1, 0);
        var far = new Piece(4, 0);
        var outside = new Piece(8, 0);
        var behind = new Piece(0, 1) { Projectable = false };
        var unsupported = new Piece(0, 2) { Selectable = false };
        var occluded = new Piece(0, 3);
        var edge = new Piece(5, 0);
        Piece.Loaded = new[] { far, outside, behind, unsupported, occluded, near, edge };
        foreach (Piece piece in Piece.Loaded) camera.Hits[piece.Screen] = piece;
        camera.Hits[occluded.Screen] = outside;
        var check = new WorldSelectionChecks();
        check.SelectBlueprintArea(camera, new Vector2(5, 5), new Vector2(-5, -5), false);
        Require(check.selected.SequenceEqual(new[] { near, far }), "Visibility, recipe filter, rectangle or nearest-first order changed.");
        Require(Piece.QueryRadius == 128 && Piece.QueryCenter.x == 48, "Loaded-area query changed.");

        check.budget = 1;
        check.selected.Clear();
        check.attempts.Clear();
        check.SelectBlueprintArea(camera, new Vector2(-5, -5), new Vector2(5, 5), false);
        Require(check.selected.SequenceEqual(new[] { near }) && check.attempts.Count == 2,
            "Selection must stop at the first budget/isolation refusal.");
        check.selected.Add(far);
        check.attempts.Clear();
        check.removing = true;
        check.SelectBlueprintArea(camera, new Vector2(-5, -5), new Vector2(5, 5), true);
        Require(check.selected.Count == 0 && check.attempts.Count == 2, "Remove must visit all visible candidates.");

        const int count = 50000;
        Piece.Loaded = Enumerable.Range(0, count).Select(i => new Piece(i / 1000f - 25, 0)).ToArray();
        camera.Hits.Clear();
        foreach (Piece piece in Piece.Loaded) camera.Hits[piece.Screen] = piece;
        check = new WorldSelectionChecks { budget = 512 };
        check.SelectBlueprintArea(camera, new Vector2(-100, -100), new Vector2(100, 100), false);
        Require(check.selected.Count == 512 && check.attempts.Count == 513, "Large batch continued after refusal.");
        Require(check.selected.Zip(check.selected.Skip(1), (a, b) => a.Screen.sqrMagnitude <= b.Screen.sqrMagnitude).All(x => x),
            "Large selection lost nearest-first ordering.");
        int measuredCalls = check.screenCalls;
        Require(measuredCalls == count,
            "Repeated renderer-bounds projection during sort: " + measuredCalls + " calls for " + count + " candidates.");
        Piece.Loaded = Array.Empty<Piece>();
        check.attempts.Clear();
        check.SelectBlueprintArea(camera, new Vector2(-1, -1), new Vector2(1, 1), false);
        Require(check.attempts.Count == 0, "Empty batch reused stale candidates.");
        return "PASS actual SelectBlueprintArea source: filtering/order/removal/refusal/empty; projections=" + measuredCalls + "/" + count + ". Host substitutes only; no Unity/Valheim run.";
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class Piece
    {
        public static Piece[] Loaded = Array.Empty<Piece>();
        public static float QueryRadius;
        public static Vector3 QueryCenter;
        public Vector2 Screen;
        public bool Selectable = true, Projectable = true;
        public Piece(float x, float y) { Screen = new Vector2(x, y); }
        public static void GetAllPiecesInRadius(Vector3 center, float radius, List<Piece> target)
        { QueryCenter = center; QueryRadius = radius; target.AddRange(Loaded); }
    }
    private sealed class Camera
    {
        public Transform transform = new Transform();
        public Dictionary<Vector2, Piece> Hits = new Dictionary<Vector2, Piece>();
    }
    private sealed class Transform { public Vector3 position = new Vector3(); public Vector3 forward = new Vector3 { x = 1 }; }
    private struct Vector3
    {
        public float x;
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3 { x = a.x + b.x }; }
        public static Vector3 operator *(Vector3 a, float b) { return new Vector3 { x = a.x * b }; }
    }
    private struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
    }
    private struct Rect
    {
        private float minX, minY, maxX, maxY;
        public static Rect MinMaxRect(float x, float y, float maxX, float maxY)
        { return new Rect { minX = x, minY = y, maxX = maxX, maxY = maxY }; }
        public Vector2 center { get { return new Vector2((minX + maxX) * .5f, (minY + maxY) * .5f); } }
        public bool Contains(Vector2 p) { return p.x >= minX && p.y >= minY && p.x < maxX && p.y < maxY; }
    }
    private static class Mathf
    {
        public static float Min(float a, float b) { return Math.Min(a, b); }
        public static float Max(float a, float b) { return Math.Max(a, b); }
    }
}
