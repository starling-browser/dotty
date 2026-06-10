using System.Runtime.InteropServices;

namespace Dotty.Terminal.Pty;

/// <summary>
/// macOS/Linux PTY using forkpty P/Invoke.
/// </summary>
public sealed partial class UnixPty : IPty
{
    private readonly int _masterFd;
    private readonly int _childPid;
    private bool _disposed;

    [DllImport("libc", SetLastError = true)]
    private static extern int forkpty(out int masterFd, IntPtr name, IntPtr termp, ref WinSize winp);

    [DllImport("libc", SetLastError = true)]
    private static extern unsafe nint read(int fd, byte* buf, nuint count);

    [DllImport("libc", SetLastError = true)]
    private static extern unsafe nint write(int fd, byte* buf, nuint count);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int waitpid(int pid, out int status, int options);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, nuint request, ref WinSize winp);

    [DllImport("libc", SetLastError = true)]
    private static extern int execvp([MarshalAs(UnmanagedType.LPUTF8Str)] string file, IntPtr[] argv);

    [DllImport("libc", SetLastError = true)]
    private static extern int setenv([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int overwrite);

    [DllImport("libc", SetLastError = true)]
    private static extern int chdir([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport("libc")]
    private static extern void _exit(int status);

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int sig);

    [DllImport("libc", SetLastError = true)]
    private static extern unsafe int tcgetattr(int fd, byte* termios);

    [DllImport("libc", SetLastError = true)]
    private static extern unsafe int tcsetattr(int fd, int optionalActions, byte* termios);

    [DllImport("libc")]
    private static extern unsafe void cfmakeraw(byte* termios);

    private const int TCSANOW = 0;

    // TIOCSWINSZ on macOS = 0x80087467, on Linux = 0x5414
    private static nuint TIOCSWINSZ => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x80087467u : 0x5414u;

    private const int WNOHANG = 1;
    private const int SIGHUP = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct WinSize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel;
        public ushort ws_ypixel;
    }

    private UnixPty(int masterFd, int childPid)
    {
        _masterFd = masterFd;
        _childPid = childPid;
    }

    /// <summary>
    /// Configures the slave PTY termios to raw mode with OPOST|ONLCR re-enabled.
    /// This matches what real terminal emulators (Alacritty, kitty) do: raw input
    /// (no echo, no canonical mode) but \n → \r\n output processing preserved.
    /// Must be called in the child process after fork, before exec.
    /// </summary>
    private static unsafe void ConfigureSlaveTermios()
    {
        // Use a raw byte buffer to avoid platform-specific struct layout issues.
        // 256 bytes is large enough for any platform's termios struct.
        byte* buf = stackalloc byte[256];
        new Span<byte>(buf, 256).Clear();

        if (tcgetattr(0, buf) != 0)
            return; // best-effort; don't fail the spawn

        cfmakeraw(buf);

        // Re-enable output processing so bare \n produces \r\n,
        // and re-enable ISIG so the kernel delivers SIGINT/SIGTSTP
        // when the user presses Ctrl+C / Ctrl+Z.
        // Field offsets and sizes differ per platform:
        //   macOS: c_iflag=0, c_oflag=8, c_cflag=16, c_lflag=24 (all ulong/8 bytes)
        //          OPOST=0x1, ONLCR=0x2, ISIG=0x80
        //   Linux: c_iflag=0, c_oflag=4, c_cflag=8, c_lflag=12 (all uint/4 bytes)
        //          OPOST=0x1, ONLCR=0x4, ISIG=0x1
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            ulong* oflag = (ulong*)(buf + 8);
            *oflag |= 0x1 | 0x2; // OPOST | ONLCR

            ulong* lflag = (ulong*)(buf + 24);
            *lflag |= 0x80; // ISIG
        }
        else
        {
            uint* oflag = (uint*)(buf + 4);
            *oflag |= 0x1 | 0x4; // OPOST | ONLCR

            uint* lflag = (uint*)(buf + 12);
            *lflag |= 0x1; // ISIG
        }

        tcsetattr(0, TCSANOW, buf);
    }

    public static UnixPty Spawn(PtyConfig config)
    {
        var winSize = new WinSize
        {
            ws_row = config.Size.Rows,
            ws_col = config.Size.Cols,
        };

        int pid = forkpty(out int masterFd, IntPtr.Zero, IntPtr.Zero, ref winSize);

        if (pid < 0)
            throw new InvalidOperationException($"forkpty failed: {Marshal.GetLastPInvokeError()}");

        if (pid == 0)
        {
            // Child process — configure PTY for raw input with output processing
            ConfigureSlaveTermios();

            string shell = config.Shell
                ?? Environment.GetEnvironmentVariable("SHELL")
                ?? "/bin/sh";

            setenv("TERM", "xterm-256color", 1);
            setenv("COLORTERM", "truecolor", 1);

            foreach (var (key, value) in config.Env)
                setenv(key, value, 1);

            if (config.WorkingDirectory != null)
                chdir(config.WorkingDirectory);

            IntPtr[] argv = [
                Marshal.StringToHGlobalAnsi(shell),
                Marshal.StringToHGlobalAnsi("-l"),
                IntPtr.Zero
            ];

            execvp(shell, argv);
            _exit(1); // execvp failed
        }

        return new UnixPty(masterFd, pid);
    }

    public unsafe int Read(Span<byte> buffer)
    {
        fixed (byte* ptr = buffer)
        {
            nint result = read(_masterFd, ptr, (nuint)buffer.Length);
            if (result < 0)
            {
                int err = Marshal.GetLastPInvokeError();
                if (err == 11 || err == 35) // EAGAIN/EWOULDBLOCK
                    return 0;
                throw new IOException($"PTY read failed: errno {err}");
            }
            return (int)result;
        }
    }

    public unsafe void Write(ReadOnlySpan<byte> data)
    {
        fixed (byte* ptr = data)
        {
            int written = 0;
            while (written < data.Length)
            {
                nint result = write(_masterFd, ptr + written, (nuint)(data.Length - written));
                if (result < 0)
                    throw new IOException($"PTY write failed: errno {Marshal.GetLastPInvokeError()}");
                written += (int)result;
            }
        }
    }

    public void Resize(GridSize size)
    {
        var ws = new WinSize { ws_row = size.Rows, ws_col = size.Cols };
        ioctl(_masterFd, TIOCSWINSZ, ref ws);
    }

    public int? TryWaitExit()
    {
        int result = waitpid(_childPid, out int status, WNOHANG);
        if (result > 0)
            return (status >> 8) & 0xFF; // WEXITSTATUS
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Kill child process first to unblock any pending read() on the master fd.
        // On macOS, close() does NOT interrupt a blocked read() from another thread.
        kill(_childPid, SIGHUP);
        close(_masterFd);

        // Reap child process to avoid zombie
        waitpid(_childPid, out _, 0);
    }
}
