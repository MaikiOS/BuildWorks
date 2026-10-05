using System;

namespace OstrixMods.BuildWorks.Geometry
{
    // Logical canvas units: reflow controls instead of shrinking the whole dialog.
    public sealed class BlueprintCatalogLayout
    {
        public double Width { get; private set; }
        public double Height { get; private set; }
        public EditorRect Navigation { get; private set; }
        public EditorRect Context { get; private set; }
        public EditorRect Grid { get; private set; }
        public EditorRect Details { get; private set; }
        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public int PageSize => Columns * Rows;
        public double CellWidth { get; private set; }
        public const double CellHeight = 152;
        public static BlueprintCatalogLayout Compute(double width, double height, bool quick, bool details)
        {
            if (!GeometryMath.IsFinite(width) || !GeometryMath.IsFinite(height) || width < 640 || height < 480)
                throw new ArgumentOutOfRangeException(nameof(width));
            var value = new BlueprintCatalogLayout { Width = Math.Min(1500, width - 24), Height = Math.Min(920, height - 24) };
            double navWidth = quick ? 0 : 154;
            double navHeight = quick ? Math.Ceiling(8 / Math.Max(1, Math.Floor((value.Width - 24) / 116))) * 40 : 0;
            double detailWidth = details && value.Width >= 1100 ? 244 : 0;
            double x = 12 + navWidth, top = 156 + navHeight;
            double available = value.Width - 24 - navWidth - detailWidth;
            value.Navigation = quick ? new EditorRect(12, 108, value.Width - 24, navHeight)
                : new EditorRect(12, 108, 142, value.Height - 178);
            value.Context = new EditorRect(x, top - 48, available, 40);
            double gridHeight = Math.Max(0, value.Height - top - 68);
            value.Columns = Math.Max(1, (int)Math.Floor((available + 8) / 128));
            value.Rows = Math.Max(1, (int)Math.Floor((gridHeight + 8) / (CellHeight + 8)));
            value.CellWidth = (available - (value.Columns - 1) * 8) / value.Columns;
            value.Grid = new EditorRect(x, top, available, value.Rows * (CellHeight + 8) - 8);
            value.Details = new EditorRect(value.Width - detailWidth - 12, 108, detailWidth, value.Height - 178);
            return value;
        }
    }
}
