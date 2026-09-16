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

# Install to PATH
WRAPPER="#!/bin/bash
exec \"$DIR/.venv/bin/python3\" \"$DIR/ImpersoTater.py\" \"\$@\""

_install_wrapper() {
    echo "$WRAPPER" > "$1"
    chmod +x "$1"
}

if [ -w /usr/local/bin ]; then
    _install_wrapper /usr/local/bin/ImpersoTater
elif command -v sudo &>/dev/null; then
    echo "$WRAPPER" | sudo tee /usr/local/bin/ImpersoTater >/dev/null
    sudo chmod +x /usr/local/bin/ImpersoTater
fi

if command -v ImpersoTater &>/dev/null; then
    echo "[+] Installed. Run with: ImpersoTater -t HOST -u USER -p PASS -c CMD"
else
    echo "[+] Installed. Run with: python3 $DIR/ImpersoTater.py -t HOST -u USER -p PASS -c CMD"
fi
