using System;
using System.Collections.Generic;
using System.Globalization;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class TransformKeyboardInput
    {
        internal readonly struct Projection
        {
            internal readonly Vector3 Axis, PlaneStart, PlaneFirst, PlaneSecond, RotationDirection;
            internal readonly Plane Plane;
            internal readonly Vector2 ScreenDirection;
            internal readonly float UnitsPerPixel;
            internal readonly bool PlaneValid, RotationValid;

            internal Projection(Camera camera, Vector2 mouse, Vector3 origin, Quaternion orientation,
                GizmoHandleKind handle, GizmoAxis first, GizmoAxis second, bool local)
            {
                Axis = first == GizmoAxis.None ? camera.transform.forward : TransformGizmoView.AxisVector(first, orientation, local);
                if (handle == GizmoHandleKind.MovePlane && second != GizmoAxis.None)
                    Axis = Vector3.Cross(Axis, TransformGizmoView.AxisVector(second, orientation, local)).normalized;
                Plane = new Plane(Axis, origin);
                Ray ray = camera.ScreenPointToRay(mouse);
                PlaneValid = Plane.Raycast(ray, out float distance);
                PlaneStart = PlaneValid ? ray.GetPoint(distance) : origin;
                PlaneFirst = Vector3.Cross(Axis,
                    Mathf.Abs(Vector3.Dot(Axis, Vector3.up)) < .9f ? Vector3.up : Vector3.right).normalized;
                PlaneSecond = Vector3.Cross(Axis, PlaneFirst).normalized;
                if (handle == GizmoHandleKind.MovePlane && second != GizmoAxis.None)
                {
                    PlaneFirst = TransformGizmoView.AxisVector(first, orientation, local);
                    PlaneSecond = TransformGizmoView.AxisVector(second, orientation, local);
                }
                Vector3 start = camera.WorldToScreenPoint(origin), end = camera.WorldToScreenPoint(origin + Axis);
                Vector2 screen = new Vector2(end.x - start.x, end.y - start.y);
                ScreenDirection = screen.normalized;
                UnitsPerPixel = 1f / Mathf.Max(2f, screen.magnitude);
                RotationDirection = Vector3.zero;
                RotationValid = false;
                if (handle == GizmoHandleKind.Rotate)
                {
                    RotationValid = TryRotationDirection(camera, mouse, origin, Axis, out Vector3 direction);
                    RotationDirection = direction;
                }
            }
        }

        internal static void SelectAxis(ref GizmoHandleKind handle, ref GizmoAxis first,
            ref GizmoAxis second, ref bool local, GizmoAxis requested, bool shift)
        {
            if (requested == first) local = !local;
            if (requested != first)
            {
                if (!shift && handle != GizmoHandleKind.Rotate && first != GizmoAxis.None && second == GizmoAxis.None)
                    second = requested;
                else { first = requested; second = GizmoAxis.None; }
            }
            if (shift) second = GizmoAxis.None;
            if (handle != GizmoHandleKind.Rotate && handle != GizmoHandleKind.Scale)
                handle = shift || second != GizmoAxis.None ? GizmoHandleKind.MovePlane : GizmoHandleKind.Move;
        }

        internal static string ReadNumber(string number, string typed, bool clear)
        {
            foreach (char character in typed ?? "")
            {
                if (character == '\b') number = number.Length == 0 ? "" : number.Substring(0, number.Length - 1);
                else if (number.Length < 32 &&
                    (character >= '0' && character <= '9' || character == '-' || character == '.' || character == ','))
                    number += character;
            }
            return clear ? "" : number;
        }

        internal static bool TryNumber(GizmoHandleKind handle, GizmoAxis first, GizmoAxis second,
            Quaternion orientation, bool local, Vector3 viewAxis, string number,
            out Vector3 translation, out Quaternion rotation, out float scale)
        {
            translation = Vector3.zero;
            rotation = Quaternion.identity;
            scale = 1f;
            if (number.EndsWith(".", StringComparison.Ordinal) || number.EndsWith(",", StringComparison.Ordinal) ||
                !float.TryParse(number.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value)) return false;
            Vector3 axis = first == GizmoAxis.None ? viewAxis : TransformGizmoView.AxisVector(first, orientation, local);
            if (handle == GizmoHandleKind.Rotate) rotation = Quaternion.AngleAxis(value, axis);
            else if (handle == GizmoHandleKind.Scale) { scale = value; return scale > 0f; }
            else
            {
                bool pair = handle == GizmoHandleKind.MovePlane && second != GizmoAxis.None;
                if (first == GizmoAxis.None || handle != GizmoHandleKind.Move && !pair) return false;
                translation = (pair ? axis + TransformGizmoView.AxisVector(second, orientation, local) : axis) * value;
                if (float.IsNaN(translation.sqrMagnitude) || float.IsInfinity(translation.sqrMagnitude)) return false;
            }
            return true;
        }

        internal static Vector3 AxisMask(GizmoAxis first, GizmoAxis second) =>
            TransformGizmoView.AxisVector(first, Quaternion.identity, false) +
            (second == GizmoAxis.None ? Vector3.zero : TransformGizmoView.AxisVector(second, Quaternion.identity, false));

        internal static Vector3 ScaleVector(GizmoHandleKind handle, GizmoAxis first, GizmoAxis second, float factor) =>
            handle == GizmoHandleKind.Scale && first != GizmoAxis.None
                ? Vector3.one + AxisMask(first, second) * (factor - 1f) : Vector3.one * factor;

        internal static Vector3 TransformPoint(Vector3 point, Vector3 pivot, Vector3 translation,
            Quaternion rotation, Vector3 scale, Quaternion scaleFrame) => pivot + rotation *
            (scaleFrame * Vector3.Scale(Quaternion.Inverse(scaleFrame) * (point - pivot), scale)) + translation;

        internal static Quaternion ScaleFrame(GizmoHandleKind handle, GizmoAxis first, bool local, Quaternion orientation) =>
            handle == GizmoHandleKind.Scale && first != GizmoAxis.None && local ? orientation : Quaternion.identity;

        internal static float AxisTranslation(Vector2 mouse, Vector2 start, Vector2 direction,
            float unitsPerPixel, float step, bool precise)
        {
            float delta = Vector2.Dot(mouse - start, direction) * unitsPerPixel;
            return precise ? delta : Mathf.Round(delta / step) * step;
        }

        internal static float MouseScale(Vector2 mouse, Vector2 start, float baseline, float stepPercent, bool precise)
        {
            float percent = mouse.y - start.y;
            if (!precise) percent = Mathf.Round(percent / stepPercent) * stepPercent;
            return baseline * Mathf.Max(.001f, 1f + percent * .01f);
        }

        internal static bool TryRotationDirection(Camera camera, Vector2 mouse, Vector3 pivot, Vector3 axis,
            out Vector3 direction)
        {
            Ray ray = camera.ScreenPointToRay(mouse);
            Plane plane = new Plane(axis, pivot);
            if (!plane.Raycast(ray, out float distance)) { direction = Vector3.zero; return false; }
            direction = ray.GetPoint(distance) - pivot;
            if (direction.sqrMagnitude < .000001f) return false;
            direction.Normalize();
            return true;
        }

        internal static float ClampMouseScale(float requested, IEnumerable<BlueprintEditorPart> parts,
            Vector3 mask, Quaternion scaleFrame)
        {
            double minimum = float.Epsilon, maximum = float.MaxValue;
            Quaternion inverse = Quaternion.Inverse(scaleFrame);
            foreach (BlueprintEditorPart part in parts)
            {
                var value = part.Rotation;
                Quaternion local = inverse * new Quaternion((float)value.X, (float)value.Y, (float)value.Z, (float)value.W);
                Limit(part.Scale.X, part.BaseScale?.X ?? 1, local * Vector3.right, part.ScaleIsAbsolute);
                Limit(part.Scale.Y, part.BaseScale?.Y ?? 1, local * Vector3.up, part.ScaleIsAbsolute);
                Limit(part.Scale.Z, part.BaseScale?.Z ?? 1, local * Vector3.forward, part.ScaleIsAbsolute);
            }
            return (float)Math.Max(minimum, Math.Min(maximum, requested));

            void Limit(double current, double basis, Vector3 axis, bool absolute)
            {
                double weight = mask == Vector3.one ? 1 :
                    mask.x * axis.x * axis.x + mask.y * axis.y * axis.y + mask.z * axis.z * axis.z;
                if (weight == 0) return;
                double lower = absolute ? float.Epsilon : BlueprintEditorDocument.MinimumScale;
                double upper = absolute ? float.MaxValue : BlueprintEditorDocument.MaximumScale;
                lower = Math.Max(lower, float.Epsilon / basis);
                upper = Math.Min(upper, float.MaxValue / basis);
                double worstFactor = 3 * BlueprintEditorDocument.MaximumScale / BlueprintEditorDocument.MinimumScale + 1;
                if (!absolute && lower == BlueprintEditorDocument.MinimumScale && upper == BlueprintEditorDocument.MaximumScale &&
                    weight * worstFactor < BlueprintEditorDocument.ScaleBoundaryRelativeTolerance * .5) return;
                minimum = Math.Max(minimum, 1 + (lower / current - 1) / weight);
                maximum = Math.Min(maximum, 1 + (upper / current - 1) / weight);
            }
        }
    }
}
