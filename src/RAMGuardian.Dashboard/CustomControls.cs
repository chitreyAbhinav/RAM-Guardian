using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using RAMGuardian.Common.Models;

namespace RAMGuardian.Dashboard
{
    public class DarkCardPanel : Panel
    {
        public int CornerRadius { get; set; }
        public Color BorderColor { get; set; }
        public Color CardBackgroundTop { get; set; }
        public Color CardBackgroundBottom { get; set; }

        public DarkCardPanel()
        {
            DoubleBuffered = true;
            CornerRadius = 10;
            BorderColor = Color.FromArgb(45, 50, 65);
            CardBackgroundTop = Color.FromArgb(28, 30, 40);
            CardBackgroundBottom = Color.FromArgb(24, 26, 35);
            Padding = new Padding(14);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GetRoundedRectangle(rect, CornerRadius))
            {
                using (var brush = new LinearGradientBrush(rect, CardBackgroundTop, CardBackgroundBottom, 90f))
                {
                    g.FillPath(brush, path);
                }
                using (var pen = new Pen(BorderColor, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        private static GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class MemoryMeterControl : Control
    {
        public double Percentage { get; set; }
        public string CenterTitle { get; set; }
        public string CenterSubtitle { get; set; }
        public Color ArcColor { get; set; }

        public MemoryMeterControl()
        {
            DoubleBuffered = true;
            Percentage = 0.0;
            CenterTitle = "0 MB";
            CenterSubtitle = "Used";
            ArcColor = Color.FromArgb(0, 180, 216);
            Size = new Size(150, 150);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int pad = 12;
            var arcRect = new Rectangle(pad, pad, Width - pad * 2, Height - pad * 2);

            // Background Track
            using (var trackPen = new Pen(Color.FromArgb(40, 44, 58), 12f))
            {
                trackPen.StartCap = LineCap.Round;
                trackPen.EndCap = LineCap.Round;
                g.DrawArc(trackPen, arcRect, 135f, 270f);
            }

            // Filled Value Arc
            float sweep = (float)(Math.Min(100.0, Math.Max(0.0, Percentage)) / 100.0 * 270.0);
            if (sweep > 0)
            {
                Color dynamicColor = ArcColor;
                if (Percentage >= 92.0) dynamicColor = Color.FromArgb(230, 57, 70); // Red
                else if (Percentage >= 80.0) dynamicColor = Color.FromArgb(244, 162, 97); // Amber

                using (var valPen = new Pen(dynamicColor, 12f))
                {
                    valPen.StartCap = LineCap.Round;
                    valPen.EndCap = LineCap.Round;
                    g.DrawArc(valPen, arcRect, 135f, sweep);
                }
            }

            // Center Text
            using (var fontTitle = new Font("Segoe UI", 13f, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (var brushText = new SolidBrush(Color.White))
            using (var brushSub = new SolidBrush(Color.FromArgb(160, 165, 185)))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                var titleRect = new Rectangle(0, Height / 2 - 18, Width, 22);
                var subRect = new Rectangle(0, Height / 2 + 6, Width, 18);

                g.DrawString(CenterTitle, fontTitle, brushText, titleRect, sf);
                g.DrawString(CenterSubtitle, fontSub, brushSub, subRect, sf);
            }
        }
    }

    public class HistoryGraphControl : Control
    {
        private readonly List<double> _availableRamHistory = new List<double>();
        private readonly List<double> _commitHistory = new List<double>();

        public HistoryGraphControl()
        {
            DoubleBuffered = true;
            Size = new Size(300, 120);
        }

        public void AddSample(double availableRamMB, double commitLoadPercent)
        {
            _availableRamHistory.Add(availableRamMB);
            _commitHistory.Add(commitLoadPercent);

            if (_availableRamHistory.Count > 40)
            {
                _availableRamHistory.RemoveAt(0);
                _commitHistory.RemoveAt(0);
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.Clear(Color.FromArgb(20, 22, 30));

            // Grid lines
            using (var gridPen = new Pen(Color.FromArgb(32, 36, 48), 1f))
            {
                gridPen.DashStyle = DashStyle.Dash;
                for (int y = 20; y < Height; y += 30)
                {
                    g.DrawLine(gridPen, 0, y, Width, y);
                }
            }

            if (_availableRamHistory.Count < 2)
            {
                using (var f = new Font("Segoe UI", 9f))
                using (var b = new SolidBrush(Color.FromArgb(120, 125, 145)))
                {
                    g.DrawString("Accumulating trend history...", f, b, 10, Height / 2 - 8);
                }
                return;
            }

            // Draw Available RAM (Cyan Line)
            float stepX = (float)Width / Math.Max(1, _availableRamHistory.Count - 1);
            var points = new PointF[_availableRamHistory.Count];

            // Max scale 2000 MB for Available RAM
            for (int i = 0; i < _availableRamHistory.Count; i++)
            {
                float x = i * stepX;
                float norm = (float)(_availableRamHistory[i] / 1500.0);
                if (norm > 1f) norm = 1f;
                float y = (Height - 10) - (norm * (Height - 20));
                points[i] = new PointF(x, y);
            }

            using (var linePen = new Pen(Color.FromArgb(0, 180, 216), 2f))
            {
                g.DrawLines(linePen, points);
            }

            // Legend
            using (var f = new Font("Segoe UI", 8f))
            using (var bCyan = new SolidBrush(Color.FromArgb(0, 180, 216)))
            {
                g.DrawString("● Available RAM (MB)", f, bCyan, 8, 4);
            }
        }
    }

    public class DarkButton : Button
    {
        public Color AccentColor { get; set; }

        public DarkButton()
        {
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 1;
            FlatAppearance.BorderColor = Color.FromArgb(50, 56, 75);
            BackColor = Color.FromArgb(32, 36, 50);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Cursor = Cursors.Hand;
            AccentColor = Color.FromArgb(0, 180, 216);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            BackColor = Color.FromArgb(42, 48, 68);
            FlatAppearance.BorderColor = AccentColor;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            BackColor = Color.FromArgb(32, 36, 50);
            FlatAppearance.BorderColor = Color.FromArgb(50, 56, 75);
        }
    }
}
