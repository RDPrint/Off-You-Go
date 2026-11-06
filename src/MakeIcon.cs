using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

class MakeIcon
{
    static void Main(string[] args)
    {
        string outputPath = args.Length > 0 ? args[0] : "app.ico";
        int[] sizes = new int[] { 16, 32, 48, 256 };
        byte[][] pngBuffers = new byte[sizes.Length][];

        for (int i = 0; i < sizes.Length; i++)
        {
            pngBuffers[i] = RenderIconPng(sizes[i]);
        }

        using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            // ICONDIR header
            bw.Write((ushort)0); // Reserved
            bw.Write((ushort)1); // Type: 1 = ICO
            bw.Write((ushort)sizes.Length); // Count

            int offset = 6 + (16 * sizes.Length);
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                bw.Write((byte)(s >= 256 ? 0 : s)); // Width
                bw.Write((byte)(s >= 256 ? 0 : s)); // Height
                bw.Write((byte)0); // Color count
                bw.Write((byte)0); // Reserved
                bw.Write((ushort)1); // Color planes
                bw.Write((ushort)32); // Bits per pixel
                bw.Write((uint)pngBuffers[i].Length); // Size of image data
                bw.Write((uint)offset); // Offset of image data
                offset += pngBuffers[i].Length;
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(pngBuffers[i]);
            }
        }
        Console.WriteLine("Generated icon at: " + outputPath);
    }

    static byte[] RenderIconPng(int size)
    {
        using (Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float pad = size * 0.04f;
            float rectSize = size - (pad * 2f);
            float cornerRadius = size * 0.18f;

            // Black glossy background button (Win7 style rounded square with dark border)
            using (GraphicsPath bgPath = CreateRoundedRect(pad, pad, rectSize, rectSize, cornerRadius))
            {
                using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                    new PointF(0, 0), new PointF(0, size),
                    Color.FromArgb(255, 38, 38, 38),
                    Color.FromArgb(255, 8, 8, 8)))
                {
                    g.FillPath(bgBrush, bgPath);
                }

                // Subtle outer border
                using (Pen borderPen = new Pen(Color.FromArgb(200, 75, 75, 75), Math.Max(1f, size * 0.03f)))
                {
                    g.DrawPath(borderPen, bgPath);
                }
            }

            // Red power symbol
            float center = size / 2f;
            float arcRadius = size * 0.27f;
            float strokeWidth = Math.Max(1.8f, size * 0.115f);

            // Outer subtle red glow
            using (Pen glowPen = new Pen(Color.FromArgb(75, 255, 20, 20), strokeWidth * 1.65f))
            {
                glowPen.StartCap = LineCap.Round;
                glowPen.EndCap = LineCap.Round;
                RectangleF arcRect = new RectangleF(center - arcRadius, center - arcRadius + (size * 0.03f), arcRadius * 2f, arcRadius * 2f);
                g.DrawArc(glowPen, arcRect, -50f, 280f);
                g.DrawLine(glowPen, center, center - arcRadius - (size * 0.06f), center, center + (size * 0.01f));
            }

            // Crisp bright red power symbol
            using (Pen redPen = new Pen(Color.FromArgb(255, 235, 30, 30), strokeWidth))
            {
                redPen.StartCap = LineCap.Round;
                redPen.EndCap = LineCap.Round;
                RectangleF arcRect = new RectangleF(center - arcRadius, center - arcRadius + (size * 0.03f), arcRadius * 2f, arcRadius * 2f);
                g.DrawArc(redPen, arcRect, -50f, 280f);
                g.DrawLine(redPen, center, center - arcRadius - (size * 0.06f), center, center + (size * 0.01f));
            }

            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }

    static GraphicsPath CreateRoundedRect(float x, float y, float width, float height, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float d = radius * 2f;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
