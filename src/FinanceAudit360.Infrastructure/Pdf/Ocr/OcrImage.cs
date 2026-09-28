namespace FinanceAudit360.Infrastructure.Pdf.Ocr;

/// <summary>Pixel preparation for OCR. Buffers are 8-bit grey, row-major, top row first.</summary>
public static class OcrImage
{
    private const byte Bright = 235;
    private const byte Dark = 110;

    /// <summary>Flattens PDFium's BGRA output onto white; unpainted areas are fully transparent.</summary>
    public static byte[] ToGray(byte[] bgra, int width, int height)
    {
        var gray = new byte[width * height];
        for (int i = 0, p = 0; i < gray.Length && p + 3 < bgra.Length; i++, p += 4)
        {
            var alpha = bgra[p + 3];
            var luma = (bgra[p] * 29 + bgra[p + 1] * 150 + bgra[p + 2] * 77) >> 8;
            gray[i] = (byte)((luma * alpha + 255 * (255 - alpha)) / 255);
        }

        return gray;
    }

    /// <summary>
    /// Finds horizontal bars shaded mid-grey (table headers, highlighted rows) and rewrites them as dark text on
    /// white: light glyphs turn black, the grey fill turns white and already-dark glyphs stay dark. Columns that
    /// are light over the full bar height are cell gaps and stay white so captions do not merge with borders.
    /// </summary>
    public static void InvertShadedBands(byte[] gray, int width, int height, double scale)
    {
        var minBandHeight = Math.Max(4, (int)(4 * scale));
        var shaded = new bool[height];
        for (var y = 0; y < height; y++)
        {
            var mid = 0;
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                var v = gray[row + x];
                if (v is >= Dark and < Bright)
                {
                    mid++;
                }
            }

            shaded[y] = mid > width * 0.3;
        }

        for (var y = 0; y < height;)
        {
            if (!shaded[y])
            {
                y++;
                continue;
            }

            var end = y;
            while (end < height && shaded[end])
            {
                end++;
            }

            if (end - y >= minBandHeight)
            {
                InvertBand(gray, width, y, end);
            }

            y = end;
        }
    }

    private static void InvertBand(byte[] gray, int width, int top, int bottom)
    {
        int left = width, right = -1;
        for (var y = top; y < bottom; y++)
        {
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                if (gray[row + x] is >= Dark and < Bright)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
            }
        }

        for (var x = left; x <= right; x++)
        {
            var gap = true;
            for (var y = top; y < bottom && gap; y++)
            {
                gap = gray[y * width + x] >= Bright;
            }

            for (var y = top; y < bottom; y++)
            {
                var i = y * width + x;
                var v = gray[i];
                gray[i] = gap || v is >= Dark and < Bright ? (byte)255 : v >= Bright ? (byte)0 : v;
            }
        }
    }

    /// <summary>Wraps a grey buffer in a 32-bit BMP, a format Leptonica reads without extra codecs.</summary>
    public static byte[] ToBitmap(byte[] gray, int width, int height, double dpi)
    {
        const int headerSize = 54;
        var pixelBytes = width * height * 4;
        var bitmap = new byte[headerSize + pixelBytes];
        var pixelsPerMetre = (int)Math.Round(dpi / 0.0254);

        bitmap[0] = (byte)'B';
        bitmap[1] = (byte)'M';
        BitConverter.TryWriteBytes(bitmap.AsSpan(2), bitmap.Length);
        BitConverter.TryWriteBytes(bitmap.AsSpan(10), headerSize);
        BitConverter.TryWriteBytes(bitmap.AsSpan(14), 40);
        BitConverter.TryWriteBytes(bitmap.AsSpan(18), width);
        BitConverter.TryWriteBytes(bitmap.AsSpan(22), -height);
        BitConverter.TryWriteBytes(bitmap.AsSpan(26), (short)1);
        BitConverter.TryWriteBytes(bitmap.AsSpan(28), (short)32);
        BitConverter.TryWriteBytes(bitmap.AsSpan(34), pixelBytes);
        BitConverter.TryWriteBytes(bitmap.AsSpan(38), pixelsPerMetre);
        BitConverter.TryWriteBytes(bitmap.AsSpan(42), pixelsPerMetre);

        for (int i = 0, p = headerSize; i < gray.Length; i++, p += 4)
        {
            bitmap[p] = bitmap[p + 1] = bitmap[p + 2] = gray[i];
            bitmap[p + 3] = 255;
        }

        return bitmap;
    }
}
