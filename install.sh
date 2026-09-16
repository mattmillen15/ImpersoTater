#!/bin/bash
# ImpersoTater dependencies

set -e

echo "[*] Installing ImpersoTater dependencies..."

# Detect package manager
if command -v apt-get &>/dev/null; then
    sudo apt-get update -qq
    sudo apt-get install -y -qq mono-mcs python3-pip
elif command -v dnf &>/dev/null; then
    sudo dnf install -y mono-core python3-pip
elif command -v pacman &>/dev/null; then
    sudo pacman -S --noconfirm mono python-pip
else
    echo "[!] Unsupported package manager. Install manually:"
    echo "    - mono (provides mcs compiler)"
    echo "    - python3 + pip"
    exit 1
fi

# Python dependency
pip3 install impacket 2>/dev/null || pip3 install impacket --break-system-packages

echo "[+] Done. Test with: mcs --version && python3 -c 'from impacket import tds; print(\"impacket OK\")'"
