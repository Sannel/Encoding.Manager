#!/usr/bin/env bash
# Installs or updates the Encoding Manager Runner as a systemd service.
# Must be run as root (sudo).
set -euo pipefail

SERVICE_NAME="sannel-encoding-runner"
UNIT_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
DEFAULT_PATH="/opt/sannel/encoding-manager/runner"
EXE_NAME="Sannel.Encoding.Runner"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

if [ "$(id -u)" -ne 0 ]; then
    echo "Error: This script must be run as root (sudo)." >&2
    exit 1
fi

INSTALL_PATH="${1:-}"
if [ -z "$INSTALL_PATH" ]; then
    printf "Install directory [%s]: " "$DEFAULT_PATH"
    read -r INSTALL_PATH
    INSTALL_PATH="${INSTALL_PATH:-$DEFAULT_PATH}"
fi

EXE_PATH="${INSTALL_PATH}/${EXE_NAME}"

if systemctl list-unit-files "${SERVICE_NAME}.service" &>/dev/null && [ -f "$UNIT_FILE" ]; then
    echo "Existing service detected — updating..."
    echo "Stopping service..."
    systemctl stop "$SERVICE_NAME" || true

    echo "Copying files to ${INSTALL_PATH}..."
    cp -af "${SCRIPT_DIR}/." "$INSTALL_PATH/"
    chmod +x "$EXE_PATH"

    echo "Reloading systemd and starting service..."
    systemctl daemon-reload
    systemctl start "$SERVICE_NAME"
    echo "Service updated and started successfully."
else
    echo "Installing Encoding Manager Runner..."

    mkdir -p "$INSTALL_PATH"

    echo "Copying files to ${INSTALL_PATH}..."
    cp -af "${SCRIPT_DIR}/." "$INSTALL_PATH/"
    chmod +x "$EXE_PATH"

    echo "Creating systemd unit file..."
    cat > "$UNIT_FILE" <<EOF
[Unit]
Description=Sannel Encoding Manager Runner
After=network.target

[Service]
Type=notify
WorkingDirectory=${INSTALL_PATH}
ExecStart=${EXE_PATH}
Restart=on-failure
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=${SERVICE_NAME}

[Install]
WantedBy=multi-user.target
EOF

    echo "Enabling and starting service..."
    systemctl daemon-reload
    systemctl enable "$SERVICE_NAME"
    systemctl start "$SERVICE_NAME"
    echo "Installation complete. Service is running."
fi

echo ""
echo "Install path : ${INSTALL_PATH}"
echo "Service name : ${SERVICE_NAME}"
echo "Executable   : ${EXE_PATH}"
echo "Unit file    : ${UNIT_FILE}"
