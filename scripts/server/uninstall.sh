#!/usr/bin/env bash
# Uninstalls the Encoding Manager Server systemd service.
# Must be run as root (sudo).
set -euo pipefail

SERVICE_NAME="sannel-encoding-server"
UNIT_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
DEFAULT_PATH="/opt/sannel/encoding-manager/server"

if [ "$(id -u)" -ne 0 ]; then
    echo "Error: This script must be run as root (sudo)." >&2
    exit 1
fi

INSTALL_PATH="${1:-}"
if [ -z "$INSTALL_PATH" ]; then
    printf "Install directory to remove [%s]: " "$DEFAULT_PATH"
    read -r INSTALL_PATH
    INSTALL_PATH="${INSTALL_PATH:-$DEFAULT_PATH}"
fi

if systemctl list-unit-files "${SERVICE_NAME}.service" &>/dev/null && [ -f "$UNIT_FILE" ]; then
    echo "Stopping service..."
    systemctl stop "$SERVICE_NAME" || true
    echo "Disabling service..."
    systemctl disable "$SERVICE_NAME" || true
    echo "Removing unit file..."
    rm -f "$UNIT_FILE"
    systemctl daemon-reload
    echo "Service removed."
else
    echo "Service '${SERVICE_NAME}' not found — skipping service removal."
fi

if [ -d "$INSTALL_PATH" ]; then
    printf "Delete install directory '%s'? [y/N]: " "$INSTALL_PATH"
    read -r confirm
    if [ "$confirm" = "y" ] || [ "$confirm" = "Y" ]; then
        rm -rf "$INSTALL_PATH"
        echo "Directory removed."
    else
        echo "Directory kept."
    fi
else
    echo "Install directory '${INSTALL_PATH}' not found — nothing to remove."
fi

echo "Uninstall complete."
