using System;

namespace OstrixMods.BuildWorks.Geometry
{
    // KG applies rootRotation * inverse(Euler(BoxRotation)); Unity Euler order is Z, X, Y.
    public static class KgBlueprintFrame
    {
        public static Rotation3 Euler(Point3 degrees)
        {
            double x = degrees.X * Math.PI / 360, y = degrees.Y * Math.PI / 360, z = degrees.Z * Math.PI / 360;
            double sx = Math.Sin(x), cx = Math.Cos(x), sy = Math.Sin(y), cy = Math.Cos(y), sz = Math.Sin(z), cz = Math.Cos(z);
            return new Rotation3(cy * sx * cz + sy * cx * sz, sy * cx * cz - cy * sx * sz,
                cy * cx * sz - sy * sx * cz, cy * cx * cz + sy * sx * sz).Normalized();
        }

        public static (Point3 Position, Rotation3 Rotation) Canonical(KgBlueprint.Piece piece, Point3 boxRotation)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            Rotation3 box = Euler(boxRotation), inverse = new Rotation3(-box.X, -box.Y, -box.Z, box.W);
            Rotation3 q = Euler(piece.Euler);
            return (inverse.Rotate(piece.Position), Compose(inverse,q));
        }

        public static (Point3 Position, Point3 Euler) Stored(Point3 position, Rotation3 rotation, Point3 boxRotation)
        {
            Rotation3 box=Euler(boxRotation);
            return (box.Rotate(position),ToEuler(Compose(box,rotation.Normalized())));
        }

        // ZXY has equivalent solutions; verify the emitted float32 orientation, not an Euler branch identity.
        public static Point3 ToEuler(Rotation3 value)
        {
            Rotation3 q=value.Normalized();
            double sinX=Math.Max(-1,Math.Min(1,2*(q.W*q.X-q.Y*q.Z)));
            double x=Math.Asin(sinX),y,z;
            if(Math.Abs(sinX)>=1-1e-12)
            { x=Math.Sign(sinX)*Math.PI/2;z=0;y=Math.Atan2(2*(q.W*q.Y-q.X*q.Z),1-2*(q.Y*q.Y+q.Z*q.Z)); }
            else
            { y=Math.Atan2(2*(q.X*q.Z+q.W*q.Y),1-2*(q.X*q.X+q.Y*q.Y));z=Math.Atan2(2*(q.X*q.Y+q.W*q.Z),1-2*(q.X*q.X+q.Z*q.Z)); }
            double Degrees(double n)=>(n*180/Math.PI+360)%360;
            return new Point3(Degrees(x),Degrees(y),Degrees(z));
        }

        private static Rotation3 Compose(Rotation3 a,Rotation3 b)=>new Rotation3(
            a.W*b.X+a.X*b.W+a.Y*b.Z-a.Z*b.Y,a.W*b.Y-a.X*b.Z+a.Y*b.W+a.Z*b.X,
            a.W*b.Z+a.X*b.Y-a.Y*b.X+a.Z*b.W,a.W*b.W-a.X*b.X-a.Y*b.Y-a.Z*b.Z).Normalized();
    }
}
