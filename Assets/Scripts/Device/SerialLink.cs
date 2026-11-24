using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif

namespace MouthOfGod.Device
{
    /// <summary>
    /// 実機の Arduino と、シリアル通信で 1 行ずつやり取りします。行の中身は知りません。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 読み書きは、専用のスレッドで行います。Unity のメインスレッドを止めません。
    /// 届いた行は、<see cref="TryDequeueLine"/> でメインスレッドから取り出します。
    /// </para>
    /// <para>
    /// COM ポートの名前は、決めません。COM ポートを順に開いて、実機の合図（<see cref="DeviceProtocol"/> の
    /// <c>(h,MOG1)</c>）が届くポートを、実機のポートとして使います。
    /// 抜けたり、行が途切れたりしたら、閉じて、探し直します。
    /// Arduino は、ポートを開くと再起動するため、1 つのポートで数秒待ちます。
    /// </para>
    /// <para>
    /// Windows のエディタとアプリだけで動きます。Quest などでは、つながらないだけで、エラーは出しません。
    /// .NET の設定を変えずに済むよう、<c>System.IO.Ports</c> は使わず、Windows の API を直接呼んでいます。
    /// </para>
    /// </remarks>
    internal sealed class SerialLink : IDisposable
    {
        private const int PumpSleepMilliseconds = 5;
        private const int ProbeMilliseconds = 4500;
        private const int RetryMilliseconds = 1000;
        private const int SilenceLimitMilliseconds = 2000;
        private const int FlushMilliseconds = 300;
        private const int MaxLineLength = 64;

        private readonly int _baudRate;
        private readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _outgoing = new ConcurrentQueue<string>();
        private readonly Thread _thread;
        private volatile bool _stopRequested;
        private volatile bool _isConnected;
        private volatile string _portName = "";
        private string _lastGoodPort = "";

        /// <param name="baudRate">通信の速さです。Arduino のスケッチと同じ値にします。</param>
        internal SerialLink(int baudRate)
        {
            _baudRate = baudRate;
            _thread = new Thread(Run) { IsBackground = true, Name = "MouthOfGod.SerialLink" };
            _thread.Start();
        }

        /// <summary>状態の行が、最近届いているとき <see langword="true"/> です。</summary>
        internal bool IsConnected => _isConnected;

        /// <summary>つながっているポートの名前です。つながっていなければ空です。</summary>
        internal string PortName => _isConnected ? _portName : "";

        /// <summary>届いた行を 1 つ取り出します。行の終わりの改行は付きません。</summary>
        /// <param name="line">取り出した行です。</param>
        /// <returns>取り出せたら <see langword="true"/> です。</returns>
        internal bool TryDequeueLine(out string line) => _incoming.TryDequeue(out line);

        /// <summary>送る行を預けます。つながっていなければ、つながったときに送ります。</summary>
        /// <param name="text">行の終わりの改行まで含めた文字です。</param>
        internal void Send(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                _outgoing.Enqueue(text);
            }
        }

        /// <summary>止める指令を送り終えるのを少し待って、通信を閉じます。</summary>
        public void Dispose()
        {
            _outgoing.Enqueue(DeviceProtocol.EncodeStop());
            Thread.Sleep(FlushMilliseconds);
            _stopRequested = true;
            _thread.Join(1000);
        }

        private void Run()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            WindowsRun();
#endif
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private void WindowsRun()
        {
            while (!_stopRequested)
            {
                IntPtr handle = ConnectToDevice();
                if (handle == InvalidHandle)
                {
                    SleepUntilStopped(RetryMilliseconds);
                    continue;
                }

                var lineBuilder = new StringBuilder();
                int lastHeardAt = Environment.TickCount;
                while (!_stopRequested)
                {
                    if (!ReadAvailable(handle, lineBuilder, out bool heardLine, out _) || !WriteAll(handle))
                    {
                        break;
                    }

                    if (heardLine)
                    {
                        lastHeardAt = Environment.TickCount;
                    }

                    if (Environment.TickCount - lastHeardAt > SilenceLimitMilliseconds)
                    {
                        break;
                    }

                    Thread.Sleep(PumpSleepMilliseconds);
                }

                _isConnected = false;
                NativeCloseHandle(handle);
            }
        }

        // 前につながったポート、残りのポートの順に、実機の合図が届くポートを探す。
        private IntPtr ConnectToDevice()
        {
            foreach (string candidate in CandidatePorts())
            {
                if (_stopRequested)
                {
                    break;
                }

                IntPtr handle = OpenPort(candidate);
                if (handle == InvalidHandle)
                {
                    continue;
                }

                if (WaitForHello(handle))
                {
                    _portName = candidate;
                    _lastGoodPort = candidate;
                    _isConnected = true;
                    return handle;
                }

                NativeCloseHandle(handle);
            }

            return InvalidHandle;
        }

        private IEnumerable<string> CandidatePorts()
        {
            var ports = new List<string>(ListPorts());
            if (_lastGoodPort.Length > 0 && ports.Remove(_lastGoodPort))
            {
                ports.Insert(0, _lastGoodPort);
            }

            return ports;
        }

        // Arduino は、ポートを開くと再起動し、数秒たってから合図を送り始める。それまで待つ。
        private bool WaitForHello(IntPtr handle)
        {
            var lineBuilder = new StringBuilder();
            int startedAt = Environment.TickCount;
            while (!_stopRequested && Environment.TickCount - startedAt < ProbeMilliseconds)
            {
                if (!ReadAvailable(handle, lineBuilder, out _, out bool heardHello))
                {
                    return false;
                }

                if (heardHello)
                {
                    return true;
                }

                Thread.Sleep(PumpSleepMilliseconds);
            }

            return false;
        }

        // 届いている分を読み、行に分けて預ける。読み取れる行が 1 つでもあれば heardLine を、実機の合図があれば heardHello を true にする。
        private bool ReadAvailable(IntPtr handle, StringBuilder lineBuilder, out bool heardLine, out bool heardHello)
        {
            heardLine = false;
            heardHello = false;
            var buffer = new byte[256];
            if (!NativeReadFile(handle, buffer, (uint)buffer.Length, out uint read, IntPtr.Zero))
            {
                return false;
            }

            for (int i = 0; i < read; i++)
            {
                char character = (char)buffer[i];
                if (character == '\n')
                {
                    string line = lineBuilder.ToString();
                    lineBuilder.Length = 0;
                    if (DeviceProtocol.TryParse(line, out DeviceMessage message))
                    {
                        heardLine = true;
                        heardHello |= message.Kind == DeviceMessageKind.Hello;
                        _incoming.Enqueue(line);
                    }
                }
                else if (character != '\r')
                {
                    if (lineBuilder.Length < MaxLineLength)
                    {
                        lineBuilder.Append(character);
                    }
                    else
                    {
                        lineBuilder.Length = 0;
                    }
                }
            }

            return true;
        }

        private bool WriteAll(IntPtr handle)
        {
            while (_outgoing.TryDequeue(out string text))
            {
                byte[] bytes = Encoding.ASCII.GetBytes(text);
                if (!NativeWriteFile(handle, bytes, (uint)bytes.Length, out _, IntPtr.Zero))
                {
                    return false;
                }
            }

            return true;
        }

        private void SleepUntilStopped(int milliseconds)
        {
            int startedAt = Environment.TickCount;
            while (!_stopRequested && Environment.TickCount - startedAt < milliseconds)
            {
                Thread.Sleep(50);
            }
        }

        private IntPtr OpenPort(string name)
        {
            IntPtr handle = NativeCreateFile(@"\\.\" + name, GenericRead | GenericWrite, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle == InvalidHandle)
            {
                return InvalidHandle;
            }

            var control = new DeviceControlBlock { DcbLength = (uint)Marshal.SizeOf(typeof(DeviceControlBlock)) };
            if (!NativeGetCommState(handle, ref control))
            {
                NativeCloseHandle(handle);
                return InvalidHandle;
            }

            control.BaudRate = (uint)_baudRate;
            control.ByteSize = 8;
            control.Parity = 0;
            control.StopBits = 0;
            control.Flags = BinaryMode | DtrEnable;

            // 読むときは、届いている分だけをすぐ返す。待たない。
            var timeouts = new CommTimeouts
            {
                ReadIntervalTimeout = uint.MaxValue,
                ReadTotalTimeoutMultiplier = 0,
                ReadTotalTimeoutConstant = 0,
                WriteTotalTimeoutMultiplier = 0,
                WriteTotalTimeoutConstant = 200,
            };

            if (!NativeSetCommState(handle, ref control) || !NativeSetCommTimeouts(handle, ref timeouts))
            {
                NativeCloseHandle(handle);
                return InvalidHandle;
            }

            NativePurgeComm(handle, PurgeAll);
            return handle;
        }

        private static IEnumerable<string> ListPorts()
        {
            var names = new List<string>();
            var buffer = new char[65536];
            uint length = NativeQueryDosDevice(null, buffer, (uint)buffer.Length);
            if (length == 0)
            {
                return names;
            }

            int start = 0;
            for (int i = 0; i < length; i++)
            {
                if (buffer[i] != '\0')
                {
                    continue;
                }

                if (i > start)
                {
                    string name = new string(buffer, start, i - start);
                    if (IsComPortName(name))
                    {
                        names.Add(name);
                    }
                }

                start = i + 1;
            }

            names.Sort((a, b) => int.Parse(a.Substring(3)).CompareTo(int.Parse(b.Substring(3))));
            return names;
        }

        private static bool IsComPortName(string name)
        {
            if (name.Length < 4 || !name.StartsWith("COM", StringComparison.Ordinal))
            {
                return false;
            }

            for (int i = 3; i < name.Length; i++)
            {
                if (name[i] < '0' || name[i] > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint OpenExisting = 3;
        private const uint PurgeAll = 0x0F;
        private const uint BinaryMode = 0x1;
        private const uint DtrEnable = 0x1 << 4;
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct DeviceControlBlock
        {
            public uint DcbLength;
            public uint BaudRate;
            public uint Flags;
            public ushort Reserved;
            public ushort XonLimit;
            public ushort XoffLimit;
            public byte ByteSize;
            public byte Parity;
            public byte StopBits;
            public byte XonChar;
            public byte XoffChar;
            public byte ErrorChar;
            public byte EofChar;
            public byte EventChar;
            public ushort Reserved1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CommTimeouts
        {
            public uint ReadIntervalTimeout;
            public uint ReadTotalTimeoutMultiplier;
            public uint ReadTotalTimeoutConstant;
            public uint WriteTotalTimeoutMultiplier;
            public uint WriteTotalTimeoutConstant;
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr NativeCreateFile(string fileName, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
        private static extern bool NativeCloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", EntryPoint = "GetCommState", SetLastError = true)]
        private static extern bool NativeGetCommState(IntPtr handle, ref DeviceControlBlock control);

        [DllImport("kernel32.dll", EntryPoint = "SetCommState", SetLastError = true)]
        private static extern bool NativeSetCommState(IntPtr handle, ref DeviceControlBlock control);

        [DllImport("kernel32.dll", EntryPoint = "SetCommTimeouts", SetLastError = true)]
        private static extern bool NativeSetCommTimeouts(IntPtr handle, ref CommTimeouts timeouts);

        [DllImport("kernel32.dll", EntryPoint = "PurgeComm", SetLastError = true)]
        private static extern bool NativePurgeComm(IntPtr handle, uint flags);

        [DllImport("kernel32.dll", EntryPoint = "ReadFile", SetLastError = true)]
        private static extern bool NativeReadFile(IntPtr handle, byte[] buffer, uint count, out uint read, IntPtr overlapped);

        [DllImport("kernel32.dll", EntryPoint = "WriteFile", SetLastError = true)]
        private static extern bool NativeWriteFile(IntPtr handle, byte[] buffer, uint count, out uint written, IntPtr overlapped);

        [DllImport("kernel32.dll", EntryPoint = "QueryDosDeviceW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint NativeQueryDosDevice(string deviceName, char[] target, uint maxLength);
#endif
    }
}
