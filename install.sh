#!/bin/bash
set -e

DIR="$(cd "$(dirname "$0")" && pwd)"

echo "[*] Installing ImpersoTater..."

# System dependency: mono mcs compiler
if ! command -v mcs &>/dev/null; then
    echo "[*] Installing mono-mcs..."
    if command -v apt-get &>/dev/null; then
        sudo apt-get update -qq
        sudo apt-get install -y -qq mono-mcs
    elif command -v dnf &>/dev/null; then
        sudo dnf install -y mono-core
    elif command -v pacman &>/dev/null; then
        sudo pacman -S --noconfirm mono
    else
        echo "[!] Install mono (provides mcs) manually"
        exit 1
    fi
fi

# Python venv + impacket
if [ ! -d "$DIR/.venv" ]; then
    echo "[*] Creating venv..."
    python3 -m venv "$DIR/.venv"
fi

echo "[*] Installing Python dependencies..."
"$DIR/.venv/bin/pip" install -q impacket

# Create wrapper script
cat > "$DIR/ImpersoTater" << 'WRAPPER'
#!/bin/bash
SELF="$0"
[ -L "$SELF" ] && SELF="$(readlink -f "$SELF")"
DIR="$(cd "$(dirname "$SELF")" && pwd)"
exec "$DIR/.venv/bin/python3" "$DIR/ImpersoTater.py" "$@"
WRAPPER
chmod +x "$DIR/ImpersoTater"

if [ -w /usr/local/bin ]; then
    ln -sf "$DIR/ImpersoTater" /usr/local/bin/ImpersoTater
elif command -v sudo &>/dev/null; then
    sudo ln -sf "$DIR/ImpersoTater" /usr/local/bin/ImpersoTater
fi

if command -v ImpersoTater &>/dev/null; then
    echo "[+] Installed. Run with: ImpersoTater -t HOST -u USER -p PASS -c CMD"
else
    echo "[+] Installed. Run with: ./ImpersoTater -t HOST -u USER -p PASS -c CMD"
fi
