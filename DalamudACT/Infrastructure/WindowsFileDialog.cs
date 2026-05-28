using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DalamudACT;

internal static class WindowsFileDialog
{
    private const int MaxPathBuffer = 4096;
    private const int OfnExplorer = 0x00080000;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnNoChangeDir = 0x00000008;

    public static bool TryPickLogFile(string initialDirectory, out string filePath, out string errorMessage)
    {
        filePath = string.Empty;
        errorMessage = string.Empty;

        if (!OperatingSystem.IsWindows())
        {
            errorMessage = "当前平台不支持 Windows 文件选择框。";
            return false;
        }

        try
        {
            var buffer = new StringBuilder(MaxPathBuffer);
            var ofn = new OpenFileName
            {
                StructSize = Marshal.SizeOf(typeof(OpenFileName)),
                File = buffer,
                MaxFile = buffer.Capacity,
                Filter = "ACT网络日志 (*.log)\0*.log\0所有文件 (*.*)\0*.*\0",
                FilterIndex = 1,
                InitialDir = initialDirectory,
                Title = "选择 ACT Network 日志文件",
                Flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir,
            };

            if (GetOpenFileName(ofn))
            {
                filePath = ofn.File.ToString();
                return true;
            }

            var dialogError = CommDlgExtendedError();
            if (dialogError != 0)
                errorMessage = $"Windows文件选择框错误：0x{dialogError:X}";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName openFileName);

    [DllImport("comdlg32.dll")]
    private static extern int CommDlgExtendedError();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class OpenFileName
    {
        public int StructSize;
        public IntPtr Owner;
        public IntPtr Instance;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? Filter;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? CustomFilter;
        public int MaxCustomFilter;
        public int FilterIndex;
        public StringBuilder? File;
        public int MaxFile;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? FileTitle;
        public int MaxFileTitle;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? InitialDir;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? Title;
        public int Flags;
        public short FileOffset;
        public short FileExtension;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? DefaultExt;
        public IntPtr CustData;
        public IntPtr Hook;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? TemplateName;
        public IntPtr ReservedPtr;
        public int ReservedInt;
        public int FlagsEx;
    }
}
