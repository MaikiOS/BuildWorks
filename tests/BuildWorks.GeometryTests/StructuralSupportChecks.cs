using System;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using Evaluation = OstrixMods.BuildWorks.Geometry.StructuralSupportEvaluation;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class StructuralSupportChecks
    {
        internal static void Run()
        {
            var wood = new Evaluation.Profile(100, 10, .2f, .125f);
            var empty = Array.Empty<Evaluation.Contact>();
            var unsupported = Evaluation.EvaluateReceiver(wood, new Point3(), false, false, empty);
            Require(unsupported.Support == 0 && !unsupported.HasSupport && unsupported.ColorValue == 0 &&
                unsupported.FirstContact == -1 && unsupported.SecondContact == -1, "Empty structure gained support");
            var ground = Evaluation.EvaluateReceiver(wood, new Point3(0, 7, 0), false, true, empty);
            Require(ground.Support == 100 && ground.HasSupport && ground.ColorValue == -1 &&
                ground.FirstContact == -1, "Verified ground contact did not give maximum support");

            var vertical = Eval(wood, Contact(new Point3(0, -2, 0), new Point3(0, -.5, 0)));
            Near(73.75f, vertical.Support);
            Require(vertical.FirstContact == 0 && vertical.SecondContact == -1, "Single support trace lost");
            Near(58f, Eval(wood, Contact(new Point3(0, -2, 0), new Point3(0, .1, 0))).Support);
            Near(70.76953f, Eval(wood, Contact(new Point3(0, -2, 0), new Point3(1, -1, 0))).Support);

            var displacedCom = new Evaluation.Contact(new Point3(), new Point3(10, 0, 0), new Point3(0, 1, 0), 100);
            Near(98f, Eval(wood, displacedCom).Support);
            Near(0f, Evaluation.EvaluateReceiver(wood, new Point3(), true, false, new[] { displacedCom }).Support);

            var left = Contact(new Point3(-1, 0, 0), new Point3(-1, 0, 0));
            var right = Contact(new Point3(1, 0, 0), new Point3(1, 0, 0));
            var bridge = Eval(wood, left, right);
            Near(86.25f, bridge.Support);
            Require(bridge.FirstContact == 0 && bridge.SecondContact == 1, "Bridge support pair trace lost");
            Near(bridge.Support, Eval(wood, right, left).Support);
            Near(78f, Eval(wood, right, Contact(new Point3(-1, 0, 0), new Point3(2, 0, 0))).Support);
            Near(78f, Eval(wood, Contact(new Point3(-1, 0, 0), new Point3(-1, .05f, 0)),
                Contact(new Point3(1, 0, 0), new Point3(1, .05f, 0))).Support);
            Near(78f, Eval(wood, left, Contact(new Point3(1, 0, 0), new Point3(1, 0, 0), relays: false)).Support);
            Near(0f, Eval(wood, Contact(new Point3(), new Point3(), relays: false)).Support);
            Near(78f, Eval(wood, left, Contact(new Point3(1, 0, 0), new Point3())).Support);
            foreach (float angle in new[] { 99.9f, 100.1f })
            {
                double radians = angle * Math.PI / 180;
                var pair = Eval(wood, right, Contact(new Point3(-1, 0, 0),
                    new Point3(Math.Cos(radians), 0, Math.Sin(radians))));
                Near(angle < 100 ? 78f : 86.25f, pair.Support);
            }

            var exactMinimum = Eval(wood, Contact(new Point3(.9f, 0, 0), new Point3(0, 1, 0), 12.5f));
            Require(exactMinimum.Support == 10 && exactMinimum.HasSupport && exactMinimum.ColorValue == 0,
                "Red at the exact native support minimum must still be stable");
            var justBelow = Eval(wood, Contact(new Point3(.9f, 0, 0), new Point3(0, 1, 0), MathF.BitDecrement(12.5f)));
            Require(justBelow.Support < 10 && !justBelow.HasSupport && justBelow.ColorValue == 0,
                "Just below native minimum was declared stable");
            var clamped = Eval(wood, Contact(new Point3(), new Point3(0, 1, 0), 1000));
            Require(clamped.Support == 100 && clamped.ColorValue == -1 && clamped.FirstContact == 0,
                "Support above the receiving material maximum was not capped");
            var half = Eval(new Evaluation.Profile(100, 10, 0, 0),
                Contact(new Point3(), new Point3(0, 1, 0), 50));
            Require(half.ColorValue == 1, "Native support color normalization changed");
            var many = Enumerable.Repeat(Contact(new Point3(.9f, 0, 0), new Point3(0, 1, 0), 12.5f), 300).ToArray();
            Near(10, Evaluation.EvaluateReceiver(wood, new Point3(), false, false, many).Support);

            // Scalar fixtures from the current SDK; profiles are supplied by the host adapter, not this table.
            foreach (var item in new[] {
                (140f, 10f, 1f / 6, .1f, 126f), (1000f, 100f, 1f, .125f, 875f),
                (1500f, 20f, 1f / 13, 1f / 13, 1384.6154f), (1500f, 100f, .5f, .125f, 1312.5f),
                (2000f, 100f, 1f / 3, .1f, 1800f), (5000f, 100f, .25f, 1f / 15, 4666.6665f),
                (1000f, 100f, 1f / 3, .125f, 875f), (200f, 10f, .2f, 1f / 13, 184.61539f) })
            {
                var profile = new Evaluation.Profile(item.Item1, item.Item2, item.Item3, item.Item4);
                var result = Eval(profile, Contact(new Point3(0, -.9f, 0), new Point3(0, -.1f, 0), item.Item1));
                Near(item.Item5, result.Support, .001f);
            }

            Reject(() => new Evaluation.Profile(float.NaN, 10, .2f, .125f));
            Reject(() => new Evaluation.Profile(100, 50, .2f, .125f));
            Reject(() => new Evaluation.Profile(100, 10, -.2f, .125f));
            Reject(() => new Evaluation.Profile(100, 10, .2f, float.PositiveInfinity));
            Reject(() => Evaluation.EvaluateReceiver(default, new Point3(), false, false, empty));
            Reject(() => Evaluation.EvaluateReceiver(wood, new Point3(), false, false, null));
            Reject(() => Evaluation.EvaluateReceiver(wood, new Point3(double.NaN, 0, 0), false, true, empty));
            Reject(() => Contact(new Point3(double.MaxValue, 0, 0), new Point3()));
            Reject(() => Contact(new Point3(), new Point3(), -1));
            Reject(() => Contact(new Point3(), new Point3(), float.NaN));
            Reject(() => Evaluation.EvaluateReceiver(wood, new Point3(-float.MaxValue, 0, 0), false, false,
                new[] { Contact(new Point3(float.MaxValue, 0, 0), new Point3()) }));
        }

        private static Evaluation.Contact Contact(Point3 origin, Point3 point, float support = 100, bool relays = true) =>
            new Evaluation.Contact(origin, origin, point, support, relays);
        private static Evaluation.Result Eval(Evaluation.Profile profile, params Evaluation.Contact[] contacts) =>
            Evaluation.EvaluateReceiver(profile, new Point3(), false, false, contacts);
        private static void Near(float expected, float actual, float tolerance = .0001f) =>
            Require(float.IsFinite(actual) && Math.Abs(expected - actual) <= tolerance, $"Support {actual} != {expected}");
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private static void Reject(Action action)
        {
            try { action(); } catch (ArgumentException) { return; }
            throw new InvalidOperationException("Invalid support input accepted");
        }
    }
}
