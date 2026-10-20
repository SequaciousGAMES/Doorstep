using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Doorstep;

internal static class ShortcutInstaller
{
    public static void Ensure(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
            return;

        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        Directory.CreateDirectory(programs);
        var desired = Path.Combine(programs, "Doorstep.lnk");
        var match = Directory.GetFiles(programs, "*.lnk")
            .FirstOrDefault(path =>
                !PathsEqual(path, desired) &&
                PathsEqual(ReadTarget(path), exePath));

        if (match != null)
        {
            File.Copy(match, desired, overwrite: true);
            if (string.Equals(Path.GetFileName(match), "Doorstep.lnk", StringComparison.OrdinalIgnoreCase))
                File.Delete(match);
            StatusLog.Write("shortcut ready " + desired);
            return;
        }

        if (File.Exists(desired) && PathsEqual(ReadTarget(desired), exePath))
        {
            WriteShortcut(desired, exePath);
            StatusLog.Write("shortcut ready " + desired);
            return;
        }

        StatusLog.Write("creating shortcut");
        WriteShortcut(desired, exePath);
        StatusLog.Write("shortcut created " + desired);
    }

    private static void WriteShortcut(string shortcutPath, string exePath)
    {
        var link = (IShellLinkW)new CShellLink();
        link.SetPath(exePath);
        link.SetArguments("");
        link.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? "");
        link.SetIconLocation(exePath, 0);
        link.SetDescription("Doorstep (unofficial)");
        link.SetShowCmd(1);

        var store = (IPropertyStore)link;
        var key = new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
        var value = PropVariant.FromString(AppIdentity.AppUserModelId);
        try
        {
            store.SetValue(ref key, ref value);
            store.Commit();
        }
        finally
        {
            PropVariantClear(ref value);
        }

        var file = (IPersistFile)link;
        file.Save(shortcutPath, true);
    }

    private static string? ReadTarget(string shortcutPath)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null)
                return null;
            dynamic shell = Activator.CreateInstance(type)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            return shortcut.TargetPath as string;
        }
        catch (Exception ex)
        {
            StatusLog.Write("shortcut read failed: " + ex.Message);
            return null;
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;

        public PropertyKey(Guid formatId, uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        public ushort VariantType;

        [FieldOffset(8)]
        public IntPtr Pointer;

        public static PropVariant FromString(string value)
        {
            return new PropVariant
            {
                VariantType = 31,
                Pointer = Marshal.StringToCoTaskMemUni(value)
            };
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink
    {
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int fileCapacity, IntPtr findData, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int nameCapacity);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int directoryCapacity);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int argumentsCapacity);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int iconCapacity, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }

    [ComImport]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }
}
