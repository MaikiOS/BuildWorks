using System;

namespace OstrixMods.BuildWorks.Geometry
{
    public readonly struct EditorRect
    {
        public EditorRect(double x, double y, double width, double height)
        {
            if (!GeometryMath.IsFinite(x) || !GeometryMath.IsFinite(y) ||
                !GeometryMath.IsFinite(width) || !GeometryMath.IsFinite(height) ||
                width < 0.0 || height < 0.0)
                throw new ArgumentOutOfRangeException(nameof(width));
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double Right => X + Width;
        public double Bottom => Y + Height;

        public bool Contains(EditorRect other) =>
            other.X >= X && other.Y >= Y &&
            other.Right <= Right && other.Bottom <= Bottom;

        public bool Overlaps(EditorRect other) =>
            X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
    }

    public sealed class BlueprintEditorLayout
    {
        public const double TopHeight = 72.0;
        public const double StatusHeight = 48.0;
        public const double RailWidth = 56.0;
        public const double StandardRightWidth = 360.0;
        public const double CompactRightWidth = 280.0;
        public const double MinimumViewportWidth = 720.0;
        public const double OutlinerHeaderHeight = 116.0;
        public const double OutlinerRowHeight = 44.0;
        public const double MinimumInspectorHeight = 440.0;
        public const int CatalogColumns = 12;
        public const int CatalogRows = 4;
        public const int CatalogPageSize = CatalogColumns * CatalogRows;
        public const double CatalogCellWidth = 84.0;
        public const double CatalogCellHeight = 96.0;
        public const double CatalogCellGap = 4.0;
        public const double CatalogGridWidth =
            CatalogColumns * CatalogCellWidth + (CatalogColumns - 1) * CatalogCellGap;
        public const double CatalogGridHeight =
            CatalogRows * CatalogCellHeight + (CatalogRows - 1) * CatalogCellGap;

        private BlueprintEditorLayout(
            EditorRect safeArea,
            EditorRect top,
            EditorRect rail,
            EditorRect viewport,
            EditorRect rightColumn,
            EditorRect outliner,
            EditorRect inspector,
            EditorRect status,
            bool compact)
        {
            SafeArea = safeArea;
            Top = top;
            Rail = rail;
            Viewport = viewport;
            RightColumn = rightColumn;
            Outliner = outliner;
            Inspector = inspector;
            Status = status;
            Compact = compact;
        }

        public EditorRect SafeArea { get; }
        public EditorRect Top { get; }
        public EditorRect Rail { get; }
        public EditorRect Viewport { get; }
        public EditorRect RightColumn { get; }
        public EditorRect Outliner { get; }
        public EditorRect Inspector { get; }
        public EditorRect Status { get; }
        public bool Compact { get; }

        public static BlueprintEditorLayout Compute(
            double logicalWidth,
            double logicalHeight)
        {
            if (!GeometryMath.IsFinite(logicalWidth) ||
                !GeometryMath.IsFinite(logicalHeight) ||
                logicalWidth < 640.0 || logicalHeight < 480.0)
                throw new ArgumentOutOfRangeException(nameof(logicalWidth));

            var safe = new EditorRect(0.0, 0.0, logicalWidth, logicalHeight);
            double bodyHeight = logicalHeight - TopHeight - StatusHeight;
            bool compact = logicalWidth < RailWidth + MinimumViewportWidth +
                StandardRightWidth;
            double rightWidth = compact ? CompactRightWidth : StandardRightWidth;
            double viewportWidth = logicalWidth - RailWidth - rightWidth;
            if (viewportWidth < 320.0)
            {
                rightWidth = Math.Max(220.0, logicalWidth - RailWidth - 320.0);
                viewportWidth = logicalWidth - RailWidth - rightWidth;
            }

            var top = new EditorRect(0.0, 0.0, logicalWidth, TopHeight);
            var rail = new EditorRect(0.0, TopHeight, RailWidth, bodyHeight);
            var viewport = new EditorRect(
                RailWidth, TopHeight, viewportWidth, bodyHeight);
            var right = new EditorRect(
                RailWidth + viewportWidth, TopHeight, rightWidth, bodyHeight);
            double desiredOutlinerBody = Math.Max(
                OutlinerRowHeight,
                Math.Min(bodyHeight * 0.44 - OutlinerHeaderHeight,
                    bodyHeight - MinimumInspectorHeight - OutlinerHeaderHeight));
            double outlinerBody = Math.Floor(desiredOutlinerBody / OutlinerRowHeight) *
                OutlinerRowHeight;
            double outlinerHeight = Math.Min(
                bodyHeight - 180.0,
                OutlinerHeaderHeight + outlinerBody);
            var outliner = new EditorRect(right.X, right.Y, right.Width, outlinerHeight);
            var inspector = new EditorRect(
                right.X,
                right.Y + outlinerHeight,
                right.Width,
                bodyHeight - outlinerHeight);
            var status = new EditorRect(
                0.0,
                logicalHeight - StatusHeight,
                logicalWidth,
                StatusHeight);
            return new BlueprintEditorLayout(
                safe, top, rail, viewport, right, outliner, inspector, status, compact);
        }
    }
}
