# ImpersoTater — Research Notes

## Problem Statement

Exploit `SeImpersonatePrivilege` on a SQL Server to escalate from the service account to `NT AUTHORITY\SYSTEM` without writing any executable binary to disk.

## Background

### SeImpersonatePrivilege

Assigned by default to Windows service accounts (`NETWORK SERVICE`, `LOCAL SERVICE`, virtual service accounts). Allows a process to impersonate any token it can obtain. The challenge is obtaining a SYSTEM token.

### Existing Tools

- **GodPotato** — Uses DCOM OXID resolver interception. Creates a TCP server that acts as a fake OXID resolver, then triggers COM unmarshaling via `Marshal.BindToMoniker`. RPCSS (SYSTEM) connects to resolve the OBJREF, giving SYSTEM authentication on a named pipe. After impersonation, enumerates all system handles via `NtQuerySystemInformation(SystemExtendedHandleInformation)` and duplicates SYSTEM tokens via `NtDuplicateObject`.

- **PrintSpoofer** — Exploits the Print Spooler service. Creates a named pipe matching `\\.\pipe\<id>\pipe\spoolss` and triggers `OpenPrinterW("\\localhost/pipe/<id>")`. The spooler connects to the pipe as SYSTEM (pre-patch) or NETWORK SERVICE (post-patch on Windows Server 2019+).

- **JuicyPotato/RoguePotato** — DCOM activation with specific CLSIDs. Requires specific COM objects to be available. Patched in modern Windows.

All require dropping an executable to disk, which triggers EDR.

## Design Decisions

### CLR Assembly Delivery

SQL Server supports loading .NET assemblies via `CREATE ASSEMBLY FROM 0x<hex>` with `PERMISSION_SET = UNSAFE`. This allows running arbitrary C# code inside `sqlservr.exe` without any file on disk (the assembly exists only in SQL Server's system catalog).

The assembly is cross-compiled on the attacker machine using Mono's `mcs` compiler targeting .NET Framework 4.x (the runtime hosted by SQL Server 2012+).

### TDS Output via SendResultsRow

SQL Server CLR procedures can return data to the client via `SqlContext.Pipe`. The `Pipe.Send(string)` method produces TDS INFO messages which impacket's `printRows()` does not capture. Using `SendResultsStart`/`SendResultsRow`/`SendResultsEnd` with a `SqlDataRecord` produces actual TDS row data that impacket handles correctly.

### Token Hunting Approach

PrintSpoofer on patched Windows Server 2019 returns `NETWORK SERVICE` instead of `SYSTEM` due to SMB loopback authentication changes. The DCOM/OXID approach fails from within SQL CLR because `Marshal.BindToMoniker` requires COM interop that the CLR host restricts.

The solution is a two-phase approach:

**Phase 1 — Trigger + Impersonate**: Use PrintSpoofer to get a pipe connection. Even though the client authenticates as `NETWORK SERVICE`, the impersonation context grants access to open other `NETWORK SERVICE` processes that share a security boundary with our service.

**Phase 2 — Handle Enumeration**: While impersonating the pipe client:
1. Call `NtQuerySystemInformation(SystemExtendedHandleInformation = 0x40)` to enumerate all handles in the system (~74K handles on a typical server)
2. For each handle in an accessible process, call `NtDuplicateObject` to duplicate it into our process
3. Call `GetTokenInformation(TokenUser)` to check if the duplicated handle is a token with SID `S-1-5-18`
4. Check the token type and impersonation level — need `TokenPrimary` or `TokenImpersonation` with `SecurityImpersonation` or higher
5. Call `DuplicateTokenEx` to create a primary token

### Process Creation

`CreateProcessWithTokenW` (via Secondary Logon Service) creates processes with the specified token but requires a proper environment block and fails with exit code 1 if the environment is missing.

`CreateProcessAsUserW` requires `SeAssignPrimaryTokenPrivilege`, which `NETWORK SERVICE` does not have. The solution: impersonate the SYSTEM token obtained from handle enumeration, then call `CreateProcessAsUserW`. While impersonating SYSTEM, the calling thread has `SeAssignPrimaryTokenPrivilege`.

The output is captured by pre-creating a temp file (writable by the service account), then redirecting the SYSTEM process's stdout to it. The temp file is deleted after reading.

## Structural Details

### _SYSTEM_HANDLE_INFORMATION_EX (64-bit)

```
Header:
  NumberOfHandles: ULONG_PTR (8 bytes, offset 0)
  Reserved:        ULONG_PTR (8 bytes, offset 8)
  Total header:    16 bytes

Each SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX (40 bytes):
  Object:               PVOID     (8 bytes, offset 0)
  UniqueProcessId:      ULONG_PTR (8 bytes, offset 8)
  HandleValue:          ULONG_PTR (8 bytes, offset 16)
  GrantedAccess:        ULONG     (4 bytes, offset 24)
  CreatorBackTraceIndex: USHORT   (2 bytes, offset 28)
  ObjectTypeIndex:      USHORT    (2 bytes, offset 30)
  HandleAttributes:     ULONG    (4 bytes, offset 32)
  Reserved:             ULONG    (4 bytes, offset 36)
```

### Key Access Rights

- `PROCESS_DUP_HANDLE` (0x0040): Required for `NtDuplicateObject` source process
- `PROCESS_QUERY_INFORMATION` (0x0400): Required to query process info
- `TOKEN_ALL_ACCESS` (0xF01FF): Full access to duplicated token
- `TOKEN_QUERY` (0x0008): Read token information

### CLR Prerequisites

- `clr enabled` must be 1
- `clr strict security` must be 0 (SQL Server 2017+) for unsigned assemblies
- Database must be `TRUSTWORTHY ON` for `UNSAFE` permission set
- Caller must be `sysadmin`

## Observations from Testing

**Target**: SQL Server 2022 Developer Edition, Windows Server 2019 Datacenter, `NT AUTHORITY\NETWORK SERVICE`, Elastic Defend enrolled.

- PrintSpoofer trigger returns `NETWORK SERVICE` on patched Windows Server 2019 (not SYSTEM). The token still provides sufficient impersonation context to open neighboring service processes.
- The handle table on a typical server has ~74K entries. Phase 1 self-scan finds ~1100 handles in the SQL Server process, none of which are SYSTEM tokens. Phase 2 finds a usable SYSTEM token within the first ~150 duplicated handles.
- PID 868 (svchost.exe hosting multiple services) held the SYSTEM impersonation token that was successfully duplicated and promoted to primary.
- `NtQuerySystemInformation` handle enumeration and `NtDuplicateObject` are not detected by Elastic Defend behavioral rules. The detection points are `CreateProcessAsUserW` (token impersonation rule) and the `xp_cmdshell` calls during enumeration.
- All Elastic Defend alerts fired in detection-only mode. No processes were blocked.
- `CreateProcessAsUserW` requires SYSTEM impersonation to succeed. Without impersonating SYSTEM first, the call fails because `NETWORK SERVICE` lacks `SeAssignPrimaryTokenPrivilege`.
- `CreateProcessWithTokenW` via SecLogon creates processes that exit with code 1 even with a proper environment block. `CreateProcessAsUserW` while impersonating SYSTEM is the reliable path.
- Output temp files created by SYSTEM are not readable by NETWORK SERVICE. Pre-creating the file as NETWORK SERVICE before launching the SYSTEM process preserves the original ACL.
