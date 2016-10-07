///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;

namespace wasSharpNET.IO
{
    /// <summary>
    ///     This is a wrapper aroung a FileStream.  While it is not a Stream itself, it can be cast to
    ///     one (keep in mind that this might throw an exception).
    /// </summary>
    public class SafeFileStream : IDisposable
    {
        #region Constructors

        public SafeFileStream(string path, FileMode mode, FileAccess access, FileShare share, uint milliscondsTimeout)
        {
            m_mutex = new Mutex(false, string.Format("Global\\{0}", path.Replace('\\', '/')));
            m_path = path;
            m_fileMode = mode;
            m_fileAccess = access;
            m_fileShare = share;
            m_millisecondsTimeout = milliscondsTimeout;
        }

        #endregion//Constructors

        #region Private Members

        private readonly Mutex m_mutex;
        private Stream m_stream;
        private readonly string m_path;
        private readonly FileMode m_fileMode;
        private readonly FileAccess m_fileAccess;
        private readonly FileShare m_fileShare;
        private readonly uint m_millisecondsTimeout;

        #endregion//Private Members

        #region Properties

        public Stream Stream
        {
            get
            {
                if (!IsOpen && !TryOpen(TimeSpan.FromMilliseconds(m_millisecondsTimeout)))
                    throw new InvalidOperationException("Timeout opening stream.");
                return m_stream;
            }
        }

        private bool IsOpen => m_stream != null;

        #endregion//Properties

        #region Functions

        /// <summary>
        ///     Opens the stream when it is not locked.  If the file is locked, then
        /// </summary>
        public void Open()
        {
            if (m_stream != null)
                throw new InvalidOperationException("The stream is already open.");
            m_mutex.WaitOne();
            m_stream = File.Open(m_path, m_fileMode, m_fileAccess, m_fileShare);
        }

        public bool TryOpen(TimeSpan span)
        {
            if (m_stream != null)
                throw new InvalidOperationException("The stream is already open.");
            if (m_mutex.WaitOne(span))
            {
                m_stream = File.Open(m_path, m_fileMode, m_fileAccess, m_fileShare);
                return true;
            }
            return false;
        }

        public void Close()
        {
            if (m_stream == null)
                return;
            m_stream.Close();
            m_stream = null;
            m_mutex.ReleaseMutex();
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }

        public static implicit operator Stream(SafeFileStream sfs)
        {
            return sfs.Stream;
        }

        #endregion//Functions
    }
}