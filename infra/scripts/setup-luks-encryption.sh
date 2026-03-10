#!/usr/bin/env bash
#
# setup-luks-encryption.sh
#
# Formats a block device with LUKS2 encryption, creates an ext4 filesystem,
# mounts it, and configures /etc/crypttab + /etc/fstab for persistent auto-mount.
#
# Designed for Hetzner Cloud volumes attached to CX-series instances running
# Ubuntu/Debian. Requires root privileges.
#
# Usage:
#   sudo ./setup-luks-encryption.sh <device> <mount-point> <mapper-name>
#
# Examples:
#   sudo ./setup-luks-encryption.sh /dev/sdb /mnt/encrypted/pgdata pgdata_crypt
#   sudo ./setup-luks-encryption.sh /dev/sdc /mnt/encrypted/redisdata redisdata_crypt
#   sudo ./setup-luks-encryption.sh /dev/sdd /mnt/encrypted/qdrantdata qdrantdata_crypt
#
# After running this script:
#   1. Update docker-compose.yml volume bindings to use the mount point.
#   2. Store the LUKS passphrase securely (password manager, vault, etc.).
#   3. Back up the LUKS header: cryptsetup luksHeaderBackup <device> --header-backup-file <file>
#
# For unattended boot, replace the passphrase with a key file:
#   dd if=/dev/urandom of=/root/.luks-keys/<mapper-name>.key bs=4096 count=1
#   chmod 400 /root/.luks-keys/<mapper-name>.key
#   cryptsetup luksAddKey <device> /root/.luks-keys/<mapper-name>.key
#   Update /etc/crypttab to reference the key file instead of 'none'.
#
# WARNING: This script will DESTROY all data on the target device.
#

set -euo pipefail

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------
LUKS_CIPHER="aes-xts-plain64"
LUKS_KEY_SIZE="512"
LUKS_HASH="sha256"
LUKS_TYPE="luks2"
FS_TYPE="ext4"

# ---------------------------------------------------------------------------
# Functions
# ---------------------------------------------------------------------------

usage() {
    echo "Usage: $0 <device> <mount-point> <mapper-name>"
    echo ""
    echo "Arguments:"
    echo "  device       Block device to encrypt (e.g., /dev/sdb)"
    echo "  mount-point  Directory to mount the filesystem (e.g., /mnt/encrypted/pgdata)"
    echo "  mapper-name  Device mapper name (e.g., pgdata_crypt)"
    echo ""
    echo "Example:"
    echo "  sudo $0 /dev/sdb /mnt/encrypted/pgdata pgdata_crypt"
    exit 1
}

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

error() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] ERROR: $1" >&2
    exit 1
}

check_root() {
    if [[ $EUID -ne 0 ]]; then
        error "This script must be run as root (use sudo)."
    fi
}

check_dependencies() {
    local missing=()
    for cmd in cryptsetup blkid mkfs.ext4 lsblk; do
        if ! command -v "$cmd" &>/dev/null; then
            missing+=("$cmd")
        fi
    done

    if [[ ${#missing[@]} -gt 0 ]]; then
        error "Missing required commands: ${missing[*]}. Install them with: apt-get install cryptsetup e2fsprogs util-linux"
    fi
}

validate_device() {
    local device="$1"

    if [[ ! -b "$device" ]]; then
        error "Device '$device' does not exist or is not a block device."
    fi

    # Check if the device is already mounted
    if mount | grep -q "^${device} "; then
        error "Device '$device' is currently mounted. Unmount it first."
    fi

    # Check if the device is already a LUKS volume
    if cryptsetup isLuks "$device" 2>/dev/null; then
        error "Device '$device' is already a LUKS volume. If you want to reformat, manually run: cryptsetup luksClose <mapper-name> && wipefs -a $device"
    fi
}

validate_mapper_name() {
    local mapper_name="$1"

    if [[ -e "/dev/mapper/${mapper_name}" ]]; then
        error "Mapper name '${mapper_name}' is already in use at /dev/mapper/${mapper_name}."
    fi

    if [[ ! "$mapper_name" =~ ^[a-zA-Z0-9_-]+$ ]]; then
        error "Mapper name '${mapper_name}' contains invalid characters. Use only alphanumeric, hyphens, and underscores."
    fi
}

confirm_destructive_action() {
    local device="$1"

    echo ""
    echo "========================================================================="
    echo "  WARNING: ALL DATA ON ${device} WILL BE PERMANENTLY DESTROYED"
    echo "========================================================================="
    echo ""
    echo "Device details:"
    lsblk "$device" 2>/dev/null || true
    echo ""

    read -r -p "Type 'YES' (uppercase) to proceed: " confirmation
    if [[ "$confirmation" != "YES" ]]; then
        log "Aborted by user."
        exit 0
    fi
}

format_luks() {
    local device="$1"

    log "Formatting ${device} with LUKS2 (cipher: ${LUKS_CIPHER}, key size: ${LUKS_KEY_SIZE} bits)..."
    cryptsetup luksFormat \
        --type "$LUKS_TYPE" \
        --cipher "$LUKS_CIPHER" \
        --key-size "$LUKS_KEY_SIZE" \
        --hash "$LUKS_HASH" \
        --verify-passphrase \
        "$device"

    log "LUKS2 formatting complete."
}

open_luks() {
    local device="$1"
    local mapper_name="$2"

    log "Opening LUKS volume as /dev/mapper/${mapper_name}..."
    cryptsetup luksOpen "$device" "$mapper_name"
    log "LUKS volume opened."
}

create_filesystem() {
    local mapper_name="$1"

    log "Creating ${FS_TYPE} filesystem on /dev/mapper/${mapper_name}..."
    mkfs."$FS_TYPE" -L "$mapper_name" "/dev/mapper/${mapper_name}"
    log "Filesystem created."
}

mount_volume() {
    local mapper_name="$1"
    local mount_point="$2"

    log "Creating mount point at ${mount_point}..."
    mkdir -p "$mount_point"

    log "Mounting /dev/mapper/${mapper_name} at ${mount_point}..."
    mount "/dev/mapper/${mapper_name}" "$mount_point"
    log "Volume mounted."
}

configure_crypttab() {
    local device="$1"
    local mapper_name="$2"

    local uuid
    uuid=$(blkid -s UUID -o value "$device")

    if [[ -z "$uuid" ]]; then
        error "Could not determine UUID of ${device}. The LUKS format may have failed."
    fi

    local crypttab_entry="${mapper_name} UUID=${uuid} none luks,discard"

    # Check if entry already exists
    if grep -q "^${mapper_name} " /etc/crypttab 2>/dev/null; then
        log "Entry for '${mapper_name}' already exists in /etc/crypttab. Skipping."
        return
    fi

    log "Adding entry to /etc/crypttab..."
    echo "$crypttab_entry" >> /etc/crypttab
    log "crypttab entry added: ${crypttab_entry}"
    log "NOTE: 'none' means the passphrase will be prompted at boot. Replace with a key file path for unattended boot."
}

configure_fstab() {
    local mapper_name="$1"
    local mount_point="$2"

    local fstab_entry="/dev/mapper/${mapper_name} ${mount_point} ${FS_TYPE} defaults,noatime 0 2"

    # Check if entry already exists
    if grep -q "/dev/mapper/${mapper_name}" /etc/fstab 2>/dev/null; then
        log "Entry for '/dev/mapper/${mapper_name}' already exists in /etc/fstab. Skipping."
        return
    fi

    log "Adding entry to /etc/fstab..."
    echo "$fstab_entry" >> /etc/fstab
    log "fstab entry added: ${fstab_entry}"
}

print_summary() {
    local device="$1"
    local mount_point="$2"
    local mapper_name="$3"

    echo ""
    echo "========================================================================="
    echo "  LUKS ENCRYPTION SETUP COMPLETE"
    echo "========================================================================="
    echo ""
    echo "  Device:       ${device}"
    echo "  Mapper:       /dev/mapper/${mapper_name}"
    echo "  Mount point:  ${mount_point}"
    echo "  Filesystem:   ${FS_TYPE}"
    echo "  Cipher:       ${LUKS_CIPHER}"
    echo "  Key size:     ${LUKS_KEY_SIZE} bits"
    echo ""
    echo "  Next steps:"
    echo "    1. Back up the LUKS header:"
    echo "       cryptsetup luksHeaderBackup ${device} --header-backup-file ${mapper_name}-header.bak"
    echo ""
    echo "    2. Store the passphrase securely (password manager, vault)."
    echo ""
    echo "    3. For unattended boot, add a key file:"
    echo "       dd if=/dev/urandom of=/root/.luks-keys/${mapper_name}.key bs=4096 count=1"
    echo "       chmod 400 /root/.luks-keys/${mapper_name}.key"
    echo "       cryptsetup luksAddKey ${device} /root/.luks-keys/${mapper_name}.key"
    echo "       Then update /etc/crypttab to reference the key file."
    echo ""
    echo "    4. Update docker-compose.yml to bind volumes to ${mount_point}."
    echo ""
    echo "    5. Set appropriate ownership for the service:"
    echo "       chown -R 999:999 ${mount_point}   # PostgreSQL"
    echo "       chown -R 999:1000 ${mount_point}  # Redis"
    echo "       chown -R 1000:1000 ${mount_point} # Qdrant"
    echo ""
    echo "========================================================================="
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

main() {
    if [[ $# -ne 3 ]]; then
        usage
    fi

    local device="$1"
    local mount_point="$2"
    local mapper_name="$3"

    check_root
    check_dependencies
    validate_device "$device"
    validate_mapper_name "$mapper_name"
    confirm_destructive_action "$device"

    format_luks "$device"
    open_luks "$device" "$mapper_name"
    create_filesystem "$mapper_name"
    mount_volume "$mapper_name" "$mount_point"
    configure_crypttab "$device" "$mapper_name"
    configure_fstab "$mapper_name" "$mount_point"
    print_summary "$device" "$mount_point" "$mapper_name"
}

main "$@"
