using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using EarTrumpet.Extensions;
using Windows.Win32;
using Windows.Win32.System.LibraryLoader;
using Windows.Win32.UI.WindowsAndMessaging;

namespace EarTrumpet.Interop.Helpers;

public class IconHelper
{
    public static Icon LoadIconForTaskbar(string path, uint dpi)
    {
        Icon icon = null;
        if (path.StartsWith("pack://"))
        {
            using var stream = System.Windows.Application.GetResourceStream(new Uri(path)).Stream;
            icon = new Icon(
                stream,
                new Size(
                    PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXICON, dpi),
                    PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CYICON, dpi)
                )
            );
        }
        else
        {
            var iconIndex = 0;
            var iconPath = path.AsSpan();
            unsafe
            {
                fixed (char* iconPathPtr = iconPath)
                {
                    iconIndex = PInvoke.PathParseIconLocation(iconPathPtr);
                }
            }

            icon = LoadIconFallback(
                iconPath.ToString(),
                iconIndex,
                PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, dpi),
                PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CYSMICON, dpi)
            );
        }
        Trace.WriteLine($"IconHelper LoadIconForTaskbar {path} {icon?.Width}x{icon?.Height}");
        return icon;
    }

    public static Icon LoadIconFallback(string path, int iconOrdinal, int cx, int cy)
    {
        try
        {
            SndVolSSO.IconId iconId = (SndVolSSO.IconId)iconOrdinal;
            string iconChar = "";
            string fontFamilyName = "Segoe Fluent Icons";

            switch (iconId)
            {
                case SndVolSSO.IconId.Muted:
                    iconChar = "\ue74f";
                    break;
                case SndVolSSO.IconId.SpeakerZeroBars:
                    iconChar = "\ue992";
                    break;
                case SndVolSSO.IconId.SpeakerOneBar:
                    iconChar = "\ue993";
                    break;
                case SndVolSSO.IconId.SpeakerTwoBars:
                    iconChar = "\ue994";
                    break;
                case SndVolSSO.IconId.SpeakerThreeBars:
                    iconChar = "\ue995";
                    break;
                case SndVolSSO.IconId.NoDevice:
                    break;
            }

            using var bmp = new Bitmap(cx, cy, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;

                using var font = new Font(
                    fontFamilyName,
                    cx * 1.0f,
                    FontStyle.Regular,
                    GraphicsUnit.Pixel
                );

                var brush = Brushes.White;
                g.Clear(Color.Transparent);

                var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Near,
                };

                g.DrawString(iconChar, font, brush, new RectangleF(0, 0, cx, cy), format);
            }

            HICON hIcon = (HICON)bmp.GetHicon();
            try
            {
                var icon = Icon.FromHandle(hIcon);
                return (Icon)icon.Clone();
            }
            finally
            {
                PInvoke.DestroyIcon(hIcon);
            }
        }
        catch
        {
            return LoadIconResource(path, iconOrdinal, cx, cy);
        }
    }

    public static Icon LoadIconResource(string path, int iconOrdinal, int cx, int cy)
    {
        using var hModule = PInvoke.LoadLibraryEx(
            path,
            LOAD_LIBRARY_FLAGS.LOAD_LIBRARY_AS_DATAFILE
                | LOAD_LIBRARY_FLAGS.LOAD_LIBRARY_AS_IMAGE_RESOURCE
        );
        unsafe
        {
            var rawModuleHandle = new HMODULE(hModule.DangerousGetHandle().ToPointer());
            var groupResInfo = PInvoke.FindResource(
                rawModuleHandle,
                new PCWSTR((char*)iconOrdinal),
                PInvoke.RT_GROUP_ICON
            );
            var groupResData = PInvoke.LockResource(PInvoke.LoadResource(hModule, groupResInfo));
            var iconId = PInvoke.LookupIconIdFromDirectoryEx(
                (byte*)groupResData,
                true,
                cx,
                cy,
                IMAGE_FLAGS.LR_DEFAULTCOLOR
            );

            var iconResInfo = PInvoke.FindResource(
                rawModuleHandle,
                new PCWSTR((char*)iconId),
                PInvoke.RT_ICON
            );
            var iconResData = PInvoke.LockResource(PInvoke.LoadResource(hModule, iconResInfo));
            var iconResSize = PInvoke.SizeofResource(hModule, iconResInfo);
            var iconHandle = PInvoke.CreateIconFromResourceEx(
                (byte*)iconResData,
                iconResSize,
                true,
                0x00030000,
                cx,
                cy,
                IMAGE_FLAGS.LR_DEFAULTCOLOR
            );

            return Icon.FromHandle(iconHandle).AsDisposableIcon();
        }
    }

    public static Icon ColorIcon(
        Icon originalIcon,
        double fillPercent,
        System.Windows.Media.Color newColor
    )
    {
        using var bitmap = originalIcon.ToBitmap();
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width * fillPercent; x++)
            {
                var pixel = bitmap.GetPixel(x, y);

                if (pixel.R > 220)
                {
                    bitmap.SetPixel(
                        x,
                        y,
                        Color.FromArgb(pixel.A, newColor.R, newColor.G, newColor.B)
                    );
                }
            }
        }

        return Icon.FromHandle(bitmap.GetHicon()).AsDisposableIcon();
    }
}
