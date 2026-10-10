using System;
using OstrixMods.BuildWorks.Geometry;

internal static class BlueprintAxisScaleTests
{
    internal static void Run()
    {
        var identity = new Rotation3(0, 0, 0, 1);
        var quarterTurn = new Rotation3(0, Math.Sqrt(.5), 0, Math.Sqrt(.5));
        var diagonal = new Rotation3(0, Math.Sin(Math.PI / 8), 0, Math.Cos(Math.PI / 8));
        var group = new BlueprintEditorGroup("group", "Group");
        var parts = new[]
        {
            new BlueprintEditorPart("a", "wall", "A", new Point3(1, 2, 3), identity, "group",
                baseScale: new Point3(.5, 2, 1), materialSeed: 7, signText: "Keep"),
            new BlueprintEditorPart("b", "wall", "B", new Point3(-2, 1, 4), quarterTurn, "group")
        };
        var doc = new BlueprintEditorDocument(null, "XYZ", "Other", parts, new[] { group });
        doc.SelectOnly("group"); doc.MarkClean();
        var scale = new Point3(2, .5, 3);
        var pivot = new Point3(1, 1, 1);
        var translation = new Point3(3, -1, 2);
        Require(doc.ApplyTransformDelta(translation, diagonal, pivot, scale, identity));
        Near(new Point3(2, .5, 3), doc.Parts[0].Scale);
        Near(new Point3(3, .5, 2), doc.Parts[1].Scale);
        for (int index = 0; index < parts.Length; ++index)
        {
            var preview = BlueprintEditorDocument.TransformPart(parts[index], translation, diagonal, pivot, scale, identity);
            Near(doc.Parts[index].Position, preview.Position);
            Near(doc.Parts[index].Scale, preview.Scale);
            Near(doc.Parts[index].Rotation.Rotate(new Point3(1, 2, 3)), preview.Rotation.Rotate(new Point3(1, 2, 3)));
            Near(new Point3(1, 1, 1), parts[index].Scale);
        }
        for (int index = 0; index < parts.Length; ++index)
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Point3 before = parts[index].Position + parts[index].Rotation.Rotate(new Point3(x, y, z));
            Point3 offset = before - pivot;
            Point3 expected = pivot + diagonal.Rotate(new Point3(offset.X * 2, offset.Y * .5, offset.Z * 3)) + translation;
            var after = doc.Parts[index];
            Point3 actual = after.Position + after.Rotation.Rotate(new Point3(x * after.Scale.X, y * after.Scale.Y, z * after.Scale.Z));
            Near(expected, actual);
        }
        Require(doc.Parts[0].MaterialSeed == 7 && doc.Parts[0].SignText == "Keep");
        Near(new Point3(.5, 2, 1), doc.Parts[0].BaseScale.Value);
        Require(doc.Undo() && !doc.IsDirty);
        Require(doc.Redo()); Near(scale, doc.Parts[0].Scale);
        Require(doc.Undo());

        // A shared rotated frame makes its aligned parts representable.
        var rotated = new BlueprintEditorDocument(null, "Rotated", "Other", new[] {
            new BlueprintEditorPart("p", "wall", "P", diagonal.Rotate(new Point3(2, 0, 0)), diagonal) });
        rotated.SelectOnly("p");
        Require(rotated.ApplyTransformDelta(new Point3(), identity, new Point3(), scale, diagonal));
        Near(diagonal.Rotate(new Point3(4, 0, 0)), rotated.Parts[0].Position);
        Near(scale, rotated.Parts[0].Scale);

        // Rotation inside an equally scaled plane remains valid.
        var equalPlane = new BlueprintEditorDocument(null, "Plane", "Other", new[] {
            new BlueprintEditorPart("p", "wall", "P", new Point3(), diagonal) });
        equalPlane.SelectOnly("p");
        Require(equalPlane.ApplyTransformDelta(new Point3(), identity, new Point3(), new Point3(2, 1, 2), identity));
        Near(new Point3(2, 1, 2), equalPlane.Parts[0].Scale);

        var nativeHalfTurn = new BlueprintEditorDocument(null, "Native half turn", "Other", new[] {
            new BlueprintEditorPart("p", "wall", "P", new Point3(), new Rotation3(0, 1, 0, -4.371138828673793e-8)) });
        nativeHalfTurn.SelectOnly("p");
        Require(nativeHalfTurn.ApplyTransformDelta(new Point3(), identity, new Point3(), new Point3(.01, 4, 1), identity));
        Near(new Point3(.01, 4, 1), nativeHalfTurn.Parts[0].Scale);

        doc.SetPartProperties("b", "B", parts[1].Position, diagonal); doc.AcceptLastEdit(); doc.MarkClean();
        Require(doc.ApplyTransformDelta(new Point3(1, 0, 0), identity, new Point3()));
        doc.AcceptLastEdit(); Require(doc.Undo());
        foreach (Point3 invalid in new[] { scale, new Point3(0, 1, 1), new Point3(-1, 1, 1),
            new Point3(double.NaN, 1, 1), new Point3(double.PositiveInfinity, 1, 1), new Point3(5, 5, 5) })
        {
            bool rejected = false;
            try { doc.ApplyTransformDelta(translation, identity, pivot, invalid, identity); }
            catch (ArgumentException) { rejected = true; }
            catch (InvalidOperationException e) { rejected = e.Message == BlueprintEditorDocument.NonUniformScaleShearError; }
            Require(rejected && !doc.IsDirty);
            Near(parts[0].Position, doc.Parts[0].Position);
            Near(new Point3(1, 1, 1), doc.Parts[0].Scale);
        }
        Require(doc.Redo()); Near(parts[0].Position + new Point3(1, 0, 0), doc.Parts[0].Position);

        var overflow = new BlueprintEditorDocument(null, "Overflow", "Other", new[] {
            new BlueprintEditorPart("a", "wall", "A", new Point3(), identity, "group"),
            new BlueprintEditorPart("b", "wall", "B", new Point3(), identity, "group",
                baseScale: new Point3(float.MaxValue / 2.0, 1, 1)) }, new[] { group });
        overflow.SelectOnly("group");
        var unchanged = overflow.Parts[0];
        bool overflowRejected = false;
        try { overflow.ApplyTransformDelta(new Point3(), identity, new Point3(), new Point3(3, 1, 1), identity); }
        catch (ArgumentOutOfRangeException) { overflowRejected = true; }
        Require(overflowRejected && !overflow.IsDirty);
        Near(new Point3(1, 1, 1), unchanged.Scale);
    }

    private static void Require(bool condition)
    { if (!condition) throw new InvalidOperationException("XYZ scale contract failed."); }

    private static void Near(Point3 expected, Point3 actual)
    { Require((expected - actual).LengthSquared < 1e-16); }
}
