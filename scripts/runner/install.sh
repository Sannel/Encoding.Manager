#!/usr/bin/env bash
# Installs or updates the Encoding Manager Runner as a systemd service.
# Must be run as root (sudo).
set -euo pipefail

SERVICE_NAME="sannel-encoding-runner"
SERVICE_USER="encode"
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

configure_handbrake_flatpak_tmp_permission() {
    local app_id="fr.handbrake.ghb"

    if ! command -v flatpak >/dev/null 2>&1; then
        return
    fi

    if ! flatpak info --system "$app_id" >/dev/null 2>&1; then
        echo "Flatpak HandBrake app '${app_id}' is not installed system-wide; skipping /tmp permission override."
        return
    fi

    echo "Granting Flatpak HandBrake access to /tmp..."
    if ! flatpak override --system --filesystem=/tmp "$app_id"; then
        echo "Warning: Failed to grant /tmp permission to Flatpak app '${app_id}'." >&2
        echo "         Run manually: flatpak override --system --filesystem=/tmp ${app_id}" >&2
    fi
}

# Ensure the service account exists (system user, no login shell, no home directory).
if ! id -u "$SERVICE_USER" &>/dev/null; then
    echo "Creating system user '${SERVICE_USER}'..."
    useradd --system --no-create-home --shell /usr/sbin/nologin "$SERVICE_USER"
fi

if systemctl list-unit-files "${SERVICE_NAME}.service" &>/dev/null && [ -f "$UNIT_FILE" ]; then
    echo "Existing service detected — updating..."
    echo "Stopping service..."
    systemctl stop "$SERVICE_NAME" || true

    echo "Copying files to ${INSTALL_PATH}..."
    cp -af "${SCRIPT_DIR}/." "$INSTALL_PATH/"
    chmod +x "$EXE_PATH"
    chown -R "${SERVICE_USER}:${SERVICE_USER}" "$INSTALL_PATH"
    configure_handbrake_flatpak_tmp_permission

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
    chown -R "${SERVICE_USER}:${SERVICE_USER}" "$INSTALL_PATH"
    configure_handbrake_flatpak_tmp_permission

    echo "Creating systemd unit file..."
    cat > "$UNIT_FILE" <<EOF
[Unit]
Description=Sannel Encoding Manager Runner
After=network.target

[Service]
Type=notify
User=${SERVICE_USER}
Group=${SERVICE_USER}
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
echo "Service user : ${SERVICE_USER}"
echo "Executable   : ${EXE_PATH}"
echo "Unit file    : ${UNIT_FILE}"
