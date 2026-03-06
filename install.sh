#!/bin/bash
set -euo pipefail

REPO="finkoslo/tripletex-employee-cli"
ASSET_PREFIX="tripletex-employee"
BINARY="finkletex"
INSTALL_DIR="/usr/local/bin"

echo "Installing $BINARY..."

OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS" in
  Darwin) PLATFORM="osx" ;;
  Linux)  PLATFORM="linux" ;;
  *)      echo "Unsupported OS: $OS"; exit 1 ;;
esac

case "$ARCH" in
  arm64|aarch64) ARCH_SUFFIX="arm64" ;;
  x86_64)        ARCH_SUFFIX="x64" ;;
  *)             echo "Unsupported architecture: $ARCH"; exit 1 ;;
esac

ASSET="$ASSET_PREFIX-$PLATFORM-$ARCH_SUFFIX.tar.gz"
TAG=$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" | grep '"tag_name"' | cut -d'"' -f4)
URL="https://github.com/$REPO/releases/download/$TAG/$ASSET"

TMPDIR=$(mktemp -d)
trap 'rm -rf "$TMPDIR"' EXIT

echo "Downloading $ASSET ($TAG)..."
curl -fsSL -o "$TMPDIR/$ASSET" "$URL"

echo "Extracting..."
tar xzf "$TMPDIR/$ASSET" -C "$TMPDIR"

echo "Installing to $INSTALL_DIR (may require sudo)..."
if [ -w "$INSTALL_DIR" ]; then
  mv "$TMPDIR/$BINARY" "$INSTALL_DIR/$BINARY"
else
  sudo mv "$TMPDIR/$BINARY" "$INSTALL_DIR/$BINARY"
fi
chmod +x "$INSTALL_DIR/$BINARY"

if [ "$PLATFORM" = "osx" ]; then
  xattr -d com.apple.quarantine "$INSTALL_DIR/$BINARY" 2>/dev/null || true
fi

echo "Installed $BINARY $TAG to $INSTALL_DIR/$BINARY"
echo "Run '$BINARY --help' to get started."
