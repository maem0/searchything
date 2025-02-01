using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using IWshRuntimeLibrary;

public class IconExtractor
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;  
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

    private readonly Dictionary<string, BitmapSource> _iconCache = new();



    public static BitmapSource GetIcon(string filePath)
    {
        string targetPath = ResolveShortcut(filePath);
        if (targetPath == null) targetPath = filePath;

        SHFILEINFO shinfo = new SHFILEINFO();
        IntPtr hIcon = SHGetFileInfo(targetPath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);

        if (hIcon == IntPtr.Zero)
            return null;

        try
        {
            using (Icon icon = Icon.FromHandle(shinfo.hIcon))
            {
                Bitmap bmp = new Bitmap(32, 32);
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawIcon(icon, new Rectangle(0, 0, 32, 32));
                    }

                    using (MemoryStream memory = new MemoryStream())
                    {
                        bmp.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                        memory.Position = 0;

                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = memory;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();
                        return bitmapImage;
                    }
                }
            }
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static string ResolveShortcut(string shortcutPath)
    {
        if (!shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            IWshShell shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
            return shortcut.TargetPath;
        }
        catch
        {
            return null;
        }
    }
}
