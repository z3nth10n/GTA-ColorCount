using System;
using System.Net;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Color = zenthion.Color;
using System.Diagnostics;
using System.Reflection;

//using System.Threading.Tasks;
//using System.Collections.Concurrent;

public enum GroundType { Building, Asphalt, LightPavement, Pavement, Grass, DryGrass, Sand, Dirt, Mud, Water, Rails, Tunnel, BadCodingDark, BadCodingLight, BuildingLight }

public enum OrderingType { ByColor, ByVal, ByName }

public class Program
{
    public static Color colorToCompare = Color.white;
    public static OrderingType orderingType = OrderingType.ByVal;
    public static bool isDarkened = false, isPosterized = false, isOrdered = true, saveTexture = false;

    private static string SavingPath
    {
        get
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "texture.png");
        }
    }

    public static void Main()
    {
        byte[] imageBytes = null;

        // OriginalTexture: http://i.imgur.com/g9fRYbm.png
        // TextureColor: https://image.ibb.co/dP3Nvf/texture-Color.png

        string url = "https://image.ibb.co/dP3Nvf/texture-Color.png";

        using (var webClient = new WebClient())
            imageBytes = webClient.DownloadData(url);

        Stopwatch sw = Stopwatch.StartNew();

        isDarkened = url == "https://image.ibb.co/dP3Nvf/texture-Color.png"; ;

        IEnumerable<Color> colors = null;

        Bitmap bitmap = null;
        var dict = GetColorCount(ref bitmap, imageBytes, (isDarkened ? F.DarkenedMapColors : F.mapColors).Values.AsEnumerable(), out colors, isPosterized);

        Console.WriteLine(DebugDict(dict));
        Console.WriteLine("Num of colors: {0}", dict.Keys.Count);

        if (saveTexture)
            colors.ToArray().SaveBitmap(7000, 5000, SavingPath);

        bitmap.Dispose();
        sw.Stop();

        Console.WriteLine("Ellapsed: {0} s", (sw.ElapsedMilliseconds / 1000f).ToString("F2"));

        Console.Read();
    }

    private static string DebugDict(Dictionary<Color, int> dict)
    {
        var num = dict
            .Select(x => new { Name = x.Key.GetGroundType(isPosterized), Similarity = x.Key.ColorSimilaryPerc(colorToCompare), Val = x.Value, ColR = x.Key.r, ColG = x.Key.g, ColB = x.Key.b })
            .GroupBy(x => x.Name)
            .Select(x => new { Name = x.Key, Similarity = x.Average(y => y.Similarity), Val = x.Sum(y => y.Val), Col = new Color((byte)x.Average(y => y.ColR), (byte)x.Average(y => y.ColG), (byte)x.Average(y => y.ColB)) });

        var num1 = num;

        if (isOrdered)
            num1 = orderingType == OrderingType.ByName ? num.OrderBy(x => x.Name) : num.OrderByDescending(x => orderingType == OrderingType.ByColor ? x.Col.ColorSimilaryPerc(colorToCompare) : x.Val);

        var num2 = num1.Select(x => string.Format("[{2}] {0}: {1}", x.Name, x.Val.ToString("N0"), x.Similarity.ToString("F2")));

        return string.Join(Environment.NewLine, num2);
    }

    public static Dictionary<Color, int> GetColorCount(ref Bitmap image, byte[] arr, IEnumerable<Color> colors, out IEnumerable<Color> imageColors, bool isPosterized = false)
    {
        Dictionary<Color, int> count = new Dictionary<Color, int>();

        using (Stream stream = new MemoryStream(arr))
            image = (Bitmap)Image.FromStream(stream);

        //Color[]
        imageColors = image.ToColor(); //.ToArray();

        //Parallel.ForEach(Partitioner.Create(imageColors, true).GetOrderableDynamicPartitions(), colorItem =>
        foreach (Color colorItem in imageColors)
        {
            // .Value
            Color thresholedColor = !isPosterized ? colorItem.GetSimilarColor(colors) : colorItem; //.RoundColorOff(65);

            if (!count.ContainsKey(thresholedColor))
                count.Add(thresholedColor, 1);
            else
                ++count[thresholedColor];
        }

        Dictionary<Color, int> posterizedColors = isPosterized ? new Dictionary<Color, int>() : count;

        if (isPosterized)
            foreach (var kv in count)
            {
                Color pColor = kv.Key.Posterize(16);

                if (!posterizedColors.ContainsKey(pColor))
                    posterizedColors.Add(pColor, kv.Value);
                else
                    posterizedColors[pColor] += kv.Value;
            }

        return posterizedColors;
    }
}

public static class F
{
    public static Dictionary<GroundType, Color> mapColors = new Dictionary<GroundType, Color>()
        {
            { GroundType.Building, Color.white },
            { GroundType.Asphalt, Color.black },
            { GroundType.LightPavement, new Color(206, 207, 206, 255) },
            { GroundType.Pavement, new Color(156, 154, 156, 255) },
            { GroundType.Grass, new Color(57, 107, 41, 255) },
            { GroundType.DryGrass, new Color(123, 148, 57, 255) },
            { GroundType.Sand, new Color(231, 190, 107, 255) },
            { GroundType.Dirt, new Color(156, 134, 115, 255) },
            { GroundType.Mud, new Color(123, 101, 90, 255) },
            { GroundType.Water, new Color(115, 138, 173, 255) },
            { GroundType.Rails, new Color(74, 4, 0, 255) },
            { GroundType.Tunnel, new Color(107, 105, 99, 255) },
            { GroundType.BadCodingDark, new Color(127, 0, 0, 255) },
            { GroundType.BadCodingLight, new Color(255, 127, 127, 255) }
        };

    private static Dictionary<GroundType, Color> _darkened;

    public static Dictionary<GroundType, Color> DarkenedMapColors
    {
        get
        {
            if (_darkened == null)
                _darkened = GetDarkenedMapColors();

            return _darkened;
        }
    }

    private static int BmpStride = 0;

    private static Dictionary<GroundType, Color> GetDarkenedMapColors()
    {
        // We will take the last 2 elements

        var last2 = mapColors.Skip(mapColors.Count - 2);

        var exceptLast2 = mapColors.Take(mapColors.Count - 2);

        Dictionary<GroundType, Color> dict = new Dictionary<GroundType, Color>();

        dict.AddRange(exceptLast2.Select(x => new KeyValuePair<GroundType, Color>(x.Key, x.Value.Lerp(Color.black, .5f))));

        dict.Add(GroundType.BuildingLight, Color.white);

        dict.AddRange(last2);

        return dict;
    }

    public static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> dic, IEnumerable<KeyValuePair<TKey, TValue>> dicToAdd)
    {
        dicToAdd.ForEach(x => dic.Add(x.Key, x.Value));
    }

    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
            action(item);
    }

    public static Color Posterize(this Color color, byte level)
    {
        byte r = 0,
             g = 0,
             b = 0;

        double value = color.r / 255.0;
        value *= level - 1;
        value = Math.Round(value);
        value /= level - 1;

        r = (byte)(value * 255);
        value = color.g / 255.0;
        value *= level - 1;
        value = Math.Round(value);
        value /= level - 1;

        g = (byte)(value * 255);
        value = color.b / 255.0;
        value *= level - 1;
        value = Math.Round(value);
        value /= level - 1;

        b = (byte)(value * 255);

        return new Color(r, g, b, 255);
    }

    public static string GetGroundType(this Color c, bool isPosterized)
    {
        var mapToUse = Program.isDarkened ? DarkenedMapColors : mapColors;
        KeyValuePair<GroundType, Color> kvColor = mapToUse.FirstOrDefault(x => isPosterized ? x.Value.ColorSimilaryPerc(c) > .9f : x.Value == c);

        if (!kvColor.Equals(default(KeyValuePair<GroundType, Color>)))
            return kvColor.Key.ToString();
        else
            return c.ToString();
    }

    public static Color GetSimilarColor(this Color c1, IEnumerable<Color> cs)
    {
        return cs.OrderBy(x => x.ColorThreshold(c1)).FirstOrDefault();
    }

    public static int ColorThreshold(this Color c1, Color c2)
    {
        return (Math.Abs(c1.r - c2.r) + Math.Abs(c1.g - c2.g) + Math.Abs(c1.b - c2.b));
    }

    public static float ColorSimilaryPerc(this Color a, Color b)
    {
        return 1f - (a.ColorThreshold(b) / (256f * 3));
    }

    public static Color RoundColorOff(this Color c, byte roundTo = 5)
    {
        return new Color(
            c.r.RoundOff(roundTo),
            c.g.RoundOff(roundTo),
            c.b.RoundOff(roundTo),
            255);
    }

    public static byte RoundOff(this byte i, byte roundTo = 5)
    {
        return (byte)((byte)Math.Ceiling(i / (double)roundTo) * roundTo);
    }

    public static IEnumerable<Color> ToColor(this Bitmap bmp)
    {
        Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        BitmapData bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
            bmp.PixelFormat);

        IntPtr ptr = bmpData.Scan0;

        int bytes = bmpData.Stride * bmp.Height;
        byte[] rgbValues = new byte[bytes];

        // Copy the RGB values into the array.
        Marshal.Copy(ptr, rgbValues, 0, bytes);

        BmpStride = bmpData.Stride;

        for (int column = 0; column < bmpData.Height; column++)
        {
            for (int row = 0; row < bmpData.Width; row++)
            {
                // Little endian
                byte b = (byte)(rgbValues[(column * BmpStride) + (row * 4)]);
                byte g = (byte)(rgbValues[(column * BmpStride) + (row * 4) + 1]);
                byte r = (byte)(rgbValues[(column * BmpStride) + (row * 4) + 2]);

                yield return new Color(r, g, b, 255);
            }
        }

        // Unlock the bits.
        bmp.UnlockBits(bmpData);
    }

    public static void SaveBitmap(this Color[] bmp, int width, int height, string path)
    {
        int stride = BmpStride;
        byte[] rgbValues = new byte[BmpStride * height];

        for (int column = 0; column < height; column++)
        {
            for (int row = 0; row < width; row++)
            {
                int i = Pn(row, column, width);

                // Little endian
                rgbValues[(column * BmpStride) + (row * 4)] = bmp[i].b;
                rgbValues[(column * BmpStride) + (row * 4) + 1] = bmp[i].g;
                rgbValues[(column * BmpStride) + (row * 4) + 2] = bmp[i].r;
                rgbValues[(column * BmpStride) + (row * 4) + 3] = bmp[i].a;
            }
        }

        unsafe
        {
            fixed (byte* ptr = rgbValues)
            {
                using (Bitmap image = new Bitmap(width, height, width * 4,
                            PixelFormat.Format32bppArgb, new IntPtr(ptr)))
                {
                    image.Save(path);
                }
            }
        }
    }

    public static int Pn(int x, int y, int w)
    {
        return x + (y * w);
    }
}

public static class Mathf
{
    public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
    {
        if (val.CompareTo(min) < 0) return min;
        else if (val.CompareTo(max) > 0) return max;
        else return val;
    }

    // Interpolates between /a/ and /b/ by /t/. /t/ is clamped between 0 and 1.
    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * Clamp01(t);
    }

    // Clamps value between 0 and 1 and returns value
    public static float Clamp01(float value)
    {
        if (value < 0F)
            return 0F;
        else if (value > 1F)
            return 1F;
        else
            return value;
    }
}

namespace zenthion
{
    /// <summary>
    /// Struct Color
    /// </summary>
    /// <seealso cref="System.ICloneable" />
    [Serializable]
    public struct Color : ICloneable
    {
        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns>System.Object.</returns>
        public object Clone()
        {
            return MemberwiseClone();
        }

        /// <summary>
        /// The r
        /// </summary>
        public byte r, g, b, a;

        /// <summary>
        /// Gets the white.
        /// </summary>
        /// <value>The white.</value>
        public static Color white
        {
            get
            {
                return new Color(255, 255, 255);
            }
        }

        /// <summary>
        /// Gets the red.
        /// </summary>
        /// <value>The red.</value>
        public static Color red
        {
            get
            {
                return new Color(255, 0, 0);
            }
        }

        /// <summary>
        /// Gets the green.
        /// </summary>
        /// <value>The green.</value>
        public static Color green
        {
            get
            {
                return new Color(0, 255, 0);
            }
        }

        /// <summary>
        /// Gets the blue.
        /// </summary>
        /// <value>The blue.</value>
        public static Color blue
        {
            get
            {
                return new Color(0, 0, 255);
            }
        }

        /// <summary>
        /// Gets the yellow.
        /// </summary>
        /// <value>The yellow.</value>
        public static Color yellow
        {
            get
            {
                return new Color(255, 255, 0);
            }
        }

        /// <summary>
        /// Gets the gray.
        /// </summary>
        /// <value>The gray.</value>
        public static Color gray
        {
            get
            {
                return new Color(128, 128, 128);
            }
        }

        /// <summary>
        /// Gets the black.
        /// </summary>
        /// <value>The black.</value>
        public static Color black
        {
            get
            {
                return new Color(0, 0, 0);
            }
        }

        /// <summary>
        /// Gets the transparent.
        /// </summary>
        /// <value>The transparent.</value>
        public static Color transparent
        {
            get
            {
                unchecked
                {
                    return new Color(0, 0, 0, 0);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="r">The r.</param>
        /// <param name="g">The g.</param>
        /// <param name="b">The b.</param>
        public Color(byte r, byte g, byte b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            a = byte.MaxValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="r">The r.</param>
        /// <param name="g">The g.</param>
        /// <param name="b">The b.</param>
        /// <param name="a">a.</param>
        public Color(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        /// <summary>
        /// Implements the ==.
        /// </summary>
        /// <param name="c1">The c1.</param>
        /// <param name="c2">The c2.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(Color c1, Color c2)
        {
            return c1.r == c2.r && c1.g == c2.g && c1.b == c2.b && c1.a == c2.a;
        }

        /// <summary>
        /// Implements the !=.
        /// </summary>
        /// <param name="c1">The c1.</param>
        /// <param name="c2">The c2.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(Color c1, Color c2)
        {
            return !(c1.r == c2.r && c1.g == c2.g && c1.b == c2.b && c1.a == c2.a);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns><c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            Color c = (Color)obj;
            return r == c.r && g == c.g && b == c.b;
        }

        /// <summary>
        /// Implements the -.
        /// </summary>
        /// <param name="c1">The c1.</param>
        /// <param name="c2">The c2.</param>
        /// <returns>The result of the operator.</returns>
        public static Color operator -(Color c1, Color c2)
        {
            return new Color(
                (byte)Mathf.Clamp(c1.r - c2.r, 0, 255),
                (byte)Mathf.Clamp(c2.g - c2.g, 0, 255),
                (byte)Mathf.Clamp(c2.b - c2.b, 0, 255));
        }

        /// <summary>
        /// Implements the +.
        /// </summary>
        /// <param name="c1">The c1.</param>
        /// <param name="c2">The c2.</param>
        /// <returns>The result of the operator.</returns>
        public static Color operator +(Color c1, Color c2)
        {
            return new Color(
                (byte)Mathf.Clamp(c1.r + c2.r, 0, 255),
                (byte)Mathf.Clamp(c2.g + c2.g, 0, 255),
                (byte)Mathf.Clamp(c2.b + c2.b, 0, 255));
        }

        /// <summary>
        /// Lerps the specified c2.
        /// </summary>
        /// <param name="c2">The c2.</param>
        /// <param name="t">The t.</param>
        /// <returns>Color.</returns>
        public Color Lerp(Color c2, float t)
        {
            return new Color(
                (byte)Mathf.Lerp(r, c2.r, t),
                (byte)Mathf.Lerp(g, c2.g, t),
                (byte)Mathf.Lerp(b, c2.b, t));
        }

        /// <summary>
        /// Inverts this instance.
        /// </summary>
        /// <returns>Color.</returns>
        public Color Invert()
        {
            return new Color(
                (byte)Mathf.Clamp(byte.MaxValue - r, 0, 255),
                (byte)Mathf.Clamp(byte.MaxValue - g, 0, 255),
                (byte)Mathf.Clamp(byte.MaxValue - b, 0, 255));
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
            if (this == white)
                return "white";
            else if (this == transparent)
                return "transparent";
            else if (this == red)
                return "red";
            else if (this == blue)
                return "blue";
            else if (this == black)
                return "black";
            else if (this == green)
                return "green";
            else if (this == yellow)
                return "yellow";
            else
                return string.Format("({0}, {1}, {2}, {3})", r, g, b, a);
        }

        /// <summary>
        /// Fills the specified x.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>Color[].</returns>
        public static IEnumerable<Color> Fill(int x, int y)
        {
            for (int i = 0; i < x * y; ++i)
                yield return black;
        }
    }
}