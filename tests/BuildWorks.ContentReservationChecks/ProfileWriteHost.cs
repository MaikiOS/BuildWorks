using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ReservationChecks
{
    internal sealed class FileWriter
    {
        public enum WriterStatus { OpenSucceeded, OpenFailed, CloseSucceeded, CloseFailed }
        private readonly string m_path;
        private readonly byte[] bytes;
        internal bool FailClose;
        public FileHelpers.FileSource m_fileSource { get; private set; } = FileHelpers.FileSource.Local;
        public WriterStatus Status { get; private set; }
        internal FileWriter(string path, byte[] bytes, WriterStatus initial = WriterStatus.OpenSucceeded)
        { m_path = path; this.bytes = bytes; Status = initial; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Finish()
        {
            if (Status == WriterStatus.CloseSucceeded) return;
            if (Status == WriterStatus.OpenFailed || FailClose) { Status = WriterStatus.CloseFailed; return; }
            using (var file = new FileStream(m_path, FileMode.Create, FileAccess.Write, FileShare.None))
            { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
            Status = WriterStatus.CloseSucceeded;
        }
    }

    internal static partial class FileHelpers
    {
        internal static bool ReplaceThrows, LeaveTemporary;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ReplaceOldFile(string saveFile, string newFile, string oldFile, object grouping, FileSource fileSource = FileSource.Auto)
        {
            if (ReplaceThrows) throw new IOException("replacement fixture failure");
            if (LeaveTemporary) return;
            if (File.Exists(saveFile))
            {
                if (File.Exists(oldFile)) File.Delete(oldFile);
                File.Move(saveFile, oldFile);
            }
            File.Move(newFile, saveFile);
        }
    }
}
