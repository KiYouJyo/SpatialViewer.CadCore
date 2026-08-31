namespace SpatialViewer.Core;

/// <summary>Double-precision point in model/world coordinates.</summary>
public readonly record struct Point2D(double X, double Y)
{
    public static Point2D Origin => new(0, 0);
    public static Point2D operator +(Point2D point, Vector2D vector) => new(point.X + vector.X, point.Y + vector.Y);
    public static Point2D operator -(Point2D point, Vector2D vector) => new(point.X - vector.X, point.Y - vector.Y);
    public static Vector2D operator -(Point2D left, Point2D right) => new(left.X - right.X, left.Y - right.Y);
    public double DistanceTo(Point2D other) => (this - other).Length;
}

/// <summary>Double-precision vector in model/world coordinates.</summary>
public readonly record struct Vector2D(double X, double Y)
{
    public double Length => Math.Sqrt((X * X) + (Y * Y));
    public double LengthSquared => (X * X) + (Y * Y);
    public Vector2D Normalized => Length <= double.Epsilon ? this : new(X / Length, Y / Length);
    public static Vector2D operator +(Vector2D left, Vector2D right) => new(left.X + right.X, left.Y + right.Y);
    public static Vector2D operator -(Vector2D left, Vector2D right) => new(left.X - right.X, left.Y - right.Y);
    public static Vector2D operator *(Vector2D vector, double scalar) => new(vector.X * scalar, vector.Y * scalar);
}

/// <summary>Double-precision two-dimensional size.</summary>
public readonly record struct Size2D(double Width, double Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>Axis-aligned bounding box represented by its world-space extrema.</summary>
public readonly record struct BoundingBox2D(double MinX, double MinY, double MaxX, double MaxY)
{
    public static BoundingBox2D Empty => new(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);
    public bool IsEmpty => MinX > MaxX || MinY > MaxY;
    public double Width => IsEmpty ? 0 : MaxX - MinX;
    public double Height => IsEmpty ? 0 : MaxY - MinY;
    public Point2D Center => IsEmpty ? Point2D.Origin : new((MinX + MaxX) / 2, (MinY + MaxY) / 2);
    public static BoundingBox2D FromPoints(IEnumerable<Point2D> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var bounds = Empty;
        foreach (var point in points) bounds = bounds.Include(point);
        return bounds;
    }
    public BoundingBox2D Include(Point2D point) => IsEmpty
        ? new(point.X, point.Y, point.X, point.Y)
        : new(Math.Min(MinX, point.X), Math.Min(MinY, point.Y), Math.Max(MaxX, point.X), Math.Max(MaxY, point.Y));
    public BoundingBox2D Union(BoundingBox2D other) => other.IsEmpty ? this : IsEmpty ? other : new(Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY), Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY));
    public BoundingBox2D Inflate(double amount) => IsEmpty ? this : new(MinX - amount, MinY - amount, MaxX + amount, MaxY + amount);
    public bool Contains(Point2D point) => !IsEmpty && point.X >= MinX && point.X <= MaxX && point.Y >= MinY && point.Y <= MaxY;
    public bool Intersects(BoundingBox2D other) => !IsEmpty && !other.IsEmpty && MaxX >= other.MinX && other.MaxX >= MinX && MaxY >= other.MinY && other.MaxY >= MinY;
    public BoundingBox2D Intersection(BoundingBox2D other) => !Intersects(other)
        ? Empty
        : new(Math.Max(MinX, other.MinX), Math.Max(MinY, other.MinY), Math.Min(MaxX, other.MaxX), Math.Min(MaxY, other.MaxY));
    public BoundingBox2D Transform(Transform2D transform) => IsEmpty ? this : FromPoints(new[] { transform.Apply(new Point2D(MinX, MinY)), transform.Apply(new Point2D(MaxX, MinY)), transform.Apply(new Point2D(MaxX, MaxY)), transform.Apply(new Point2D(MinX, MaxY)) });
}

/// <summary>Immutable double-precision 2D affine transform. Composition follows <c>first.Then(second)</c>.</summary>
public readonly record struct Transform2D(double M11, double M12, double M21, double M22, double Dx, double Dy)
{
    public static Transform2D Identity => new(1, 0, 0, 1, 0, 0);
    public static Transform2D Translation(double x, double y) => new(1, 0, 0, 1, x, y);
    public static Transform2D Scale(double x, double y) => new(x, 0, 0, y, 0, 0);
    public static Transform2D Rotation(double radians)
    {
        var cosine = Math.Cos(radians); var sine = Math.Sin(radians);
        return new(cosine, sine, -sine, cosine, 0, 0);
    }
    public Point2D Apply(Point2D point) => new((point.X * M11) + (point.Y * M21) + Dx, (point.X * M12) + (point.Y * M22) + Dy);
    public Vector2D Apply(Vector2D vector) => new((vector.X * M11) + (vector.Y * M21), (vector.X * M12) + (vector.Y * M22));
    public Transform2D Then(Transform2D next) => new(
        (M11 * next.M11) + (M12 * next.M21), (M11 * next.M12) + (M12 * next.M22),
        (M21 * next.M11) + (M22 * next.M21), (M21 * next.M12) + (M22 * next.M22),
        (Dx * next.M11) + (Dy * next.M21) + next.Dx, (Dx * next.M12) + (Dy * next.M22) + next.Dy);
    public bool TryInvert(out Transform2D inverse)
    {
        var determinant = (M11 * M22) - (M12 * M21);
        if (Math.Abs(determinant) < 1e-15) { inverse = Identity; return false; }
        var reciprocal = 1 / determinant;
        inverse = new Transform2D(M22 * reciprocal, -M12 * reciprocal, -M21 * reciprocal, M11 * reciprocal, 0, 0);
        inverse = inverse with { Dx = -((Dx * inverse.M11) + (Dy * inverse.M21)), Dy = -((Dx * inverse.M12) + (Dy * inverse.M22)) };
        return true;
    }
}
