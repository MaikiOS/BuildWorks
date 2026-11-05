using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks.Geometry
{
    // One receiver update from verified contacts, not a contact finder or a whole-building solver.
    public static class StructuralSupportEvaluation
    {
        public readonly struct Profile
        {
            public Profile(float maximum, float minimum, float horizontalLoss, float verticalLoss)
            {
                Maximum = maximum;
                Minimum = minimum;
                HorizontalLoss = horizontalLoss;
                VerticalLoss = verticalLoss;
                Validate(this);
            }

            public float Maximum { get; }
            public float Minimum { get; }
            public float HorizontalLoss { get; }
            public float VerticalLoss { get; }
        }

        public readonly struct Contact
        {
            public Contact(Point3 sourceOrigin, Point3 sourceCentreOfMass, Point3 supportPoint,
                float sourceSupport, bool relaysSupport = true)
            {
                CheckPoint(sourceOrigin);
                CheckPoint(sourceCentreOfMass);
                CheckPoint(supportPoint);
                if (!Finite(sourceSupport) || sourceSupport < 0f)
                    throw new ArgumentOutOfRangeException(nameof(sourceSupport));
                SourceOrigin = sourceOrigin;
                SourceCentreOfMass = sourceCentreOfMass;
                SupportPoint = supportPoint;
                SourceSupport = sourceSupport;
                RelaysSupport = relaysSupport;
            }

            public Point3 SourceOrigin { get; }
            public Point3 SourceCentreOfMass { get; }
            public Point3 SupportPoint { get; }
            public float SourceSupport { get; }
            public bool RelaysSupport { get; }
        }

        public readonly struct Result
        {
            internal Result(Profile profile, float support, int first, int second)
            {
                Support = support;
                HasSupport = support >= profile.Minimum;
                ColorValue = support >= profile.Maximum ? -1f :
                    Clamp01((support - profile.Minimum) / (profile.Maximum * .5f - profile.Minimum));
                FirstContact = first;
                SecondContact = second;
            }

            public float Support { get; }
            public bool HasSupport { get; }
            // -1 is the native maximum-support blue; 0 red can still satisfy HasSupport.
            public float ColorValue { get; }
            public int FirstContact { get; }
            public int SecondContact { get; }
        }

        public static Result EvaluateReceiver(Profile profile, Point3 centreOfMass,
            bool forceCorrectCentreOfMass, bool grounded, IReadOnlyList<Contact> contacts)
        {
            Validate(profile);
            CheckPoint(centreOfMass);
            if (contacts == null) throw new ArgumentNullException(nameof(contacts));
            if (grounded) return new Result(profile, profile.Maximum, -1, -1);

            float support = 0f;
            int first = -1, second = -1;
            var below = new List<(int Index, float X, float Z, float Value)>();
            for (int index = 0; index < contacts.Count; ++index)
            {
                Contact contact = contacts[index];
                if (!contact.RelaysSupport) continue;
                float distance = Distance(centreOfMass, contact.SourceCentreOfMass) + .1f;
                float originDistance = Distance(centreOfMass, contact.SourceOrigin) + .1f;
                if (!forceCorrectCentreOfMass && originDistance < distance) distance = originDistance;
                if (!Finite(distance)) throw new ArgumentOutOfRangeException(nameof(contacts));
                float candidate = contact.SourceSupport - profile.HorizontalLoss * distance * contact.SourceSupport;
                Consider(candidate, index, -1);
                if ((float)contact.SupportPoint.Y >= (float)centreOfMass.Y + .05f) continue;

                float x = (float)contact.SupportPoint.X - (float)centreOfMass.X;
                float y = (float)contact.SupportPoint.Y - (float)centreOfMass.Y;
                float z = (float)contact.SupportPoint.Z - (float)centreOfMass.Z;
                float length = Sqrt(x * x + y * y + z * z);
                if (!Finite(length)) throw new ArgumentOutOfRangeException(nameof(contacts));
                float directionY = length > .00001f ? y / length : 0f;
                if (directionY < 0f)
                {
                    float blend = Clamp01((float)Math.Acos(1f - Math.Abs(directionY)) / ((float)Math.PI * .5f));
                    float loss = profile.HorizontalLoss + (profile.VerticalLoss - profile.HorizontalLoss) * blend;
                    Consider(contact.SourceSupport - loss * distance * contact.SourceSupport, index, -1);
                }
                below.Add((index, x, z,
                    contact.SourceSupport - profile.VerticalLoss * distance * contact.SourceSupport));
            }

            for (int left = 0; left < below.Count - 1; ++left)
                for (int right = left + 1; right < below.Count; ++right)
                {
                    var a = below[left];
                    var b = below[right];
                    float candidate = (a.Value + b.Value) * .5f;
                    if (candidate <= support) continue;
                    float denominator = Sqrt((a.X * a.X + a.Z * a.Z) * (b.X * b.X + b.Z * b.Z));
                    if (!Finite(denominator)) throw new ArgumentOutOfRangeException(nameof(contacts));
                    if (denominator < 1e-15f) continue;
                    float cosine = Math.Max(-1f, Math.Min(1f, (a.X * b.X + a.Z * b.Z) / denominator));
                    if ((float)Math.Acos(cosine) * 57.29578f >= 100f)
                        Consider(candidate, a.Index, b.Index);
                }
            return new Result(profile, Math.Min(support, profile.Maximum), first, second);

            void Consider(float candidate, int a, int b)
            {
                if (!Finite(candidate)) throw new ArgumentOutOfRangeException(nameof(contacts));
                if (candidate <= support) return;
                support = candidate;
                first = a;
                second = b;
            }
        }

        private static void Validate(Profile profile)
        {
            if (!Finite(profile.Maximum) || !Finite(profile.Minimum) ||
                !Finite(profile.HorizontalLoss) || !Finite(profile.VerticalLoss) ||
                profile.Minimum < 0f || profile.Maximum * .5f <= profile.Minimum ||
                profile.HorizontalLoss < 0f || profile.VerticalLoss < 0f)
                throw new ArgumentOutOfRangeException(nameof(profile), "Unsupported structural material profile.");
        }

        private static void CheckPoint(Point3 point)
        {
            if (!Finite((float)point.X) || !Finite((float)point.Y) || !Finite((float)point.Z))
                throw new ArgumentOutOfRangeException(nameof(point));
        }

        private static float Distance(Point3 a, Point3 b)
        {
            float x = (float)a.X - (float)b.X;
            float y = (float)a.Y - (float)b.Y;
            float z = (float)a.Z - (float)b.Z;
            return Sqrt(x * x + y * y + z * z);
        }

        private static float Sqrt(float value) => (float)Math.Sqrt(value);
        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
