namespace FFMediaToolkit.Helpers;

using System;
using System.Runtime.InteropServices;
using System.Text;
using FFmpeg.AutoGen;

/// <summary>
/// Contains string conversion methods.
/// </summary>
internal static class StringConverter
{
    /// <summary>
    /// Creates a new <see cref="string"/> from a pointer to the unmanaged UTF-8 string.
    /// </summary>
    /// <param name="pointer">A pointer to the unmanaged string.</param>
    /// <returns>The converted string.</returns>
    public static string Utf8ToString(this nint pointer)
    {
        int lenght = 0;

        while (Marshal.ReadByte(pointer, lenght) != 0)
        {
            ++lenght;
        }

        byte[] buffer = new byte[lenght];
        Marshal.Copy(pointer, buffer, 0, lenght);

        return Encoding.UTF8.GetString(buffer);
    }

    /// <summary>
    /// Gets the FFmpeg error message based on the error code.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <returns>The decoded error message.</returns>
    public static unsafe string DecodeMessage(int errorCode)
    {
        const int BUFFER_SIZE = 1024;
        byte* buffer = stackalloc byte[BUFFER_SIZE];
        ffmpeg.av_strerror(errorCode, buffer, BUFFER_SIZE);

        string message = new nint(buffer).Utf8ToString();
        return message;
    }
}