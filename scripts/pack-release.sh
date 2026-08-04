#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

VERSION="${1:-$(tr -d '[:space:]' < VERSION)}"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Version không hợp lệ: $VERSION"
  echo "Dùng: ./scripts/pack-release.sh 0.1.0"
  exit 1
fi

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

DIST="$ROOT/dist"
STAGE="$DIST/staging/MepPanel-v$VERSION"
SERVER_OUT="$STAGE/LicenseServer"
DOCS_OUT="$STAGE/docs"
PLUGIN_SRC_OUT="$STAGE/PluginSource"
ZIP_SERVER="$DIST/MepPanel-LicenseServer-v$VERSION.zip"
ZIP_FULL="$DIST/MepPanel-v$VERSION.zip"

echo "==> Đóng gói MepPanel v$VERSION"

rm -rf "$STAGE"
mkdir -p "$SERVER_OUT" "$DOCS_OUT" "$PLUGIN_SRC_OUT"

echo "==> Build LicenseServer (Release)"
dotnet publish "$ROOT/MepPanel.LicenseServer/MepPanel.LicenseServer.csproj" \
  -c Release \
  -o "$SERVER_OUT" \
  -p:MepPanelVersion="$VERSION" \
  -p:Version="$VERSION"

echo "==> Copy tài liệu + source plugin"
cp "$ROOT/README.md" "$STAGE/"
cp "$ROOT/CHANGELOG.md" "$STAGE/"
cp "$ROOT/VERSION" "$STAGE/"
cp "$ROOT/docs/LICENSE_ADMIN_GUIDE.md" "$DOCS_OUT/"
cp -R "$ROOT/src/MepPanel.AutoCAD/." "$PLUGIN_SRC_OUT/MepPanel.AutoCAD/"
cp -R "$ROOT/src/MepPanel.Core/." "$PLUGIN_SRC_OUT/MepPanel.Core/"

cat > "$STAGE/RELEASE_NOTES.md" <<EOF
# MepPanel v$VERSION

Ngày đóng gói: $(date -u +%Y-%m-%d)

## Thành phần trong gói

| Thư mục | Mô tả |
|---|---|
| \`LicenseServer/\` | Máy chủ kiểm soát (chạy được) |
| \`PluginSource/\` | Source plugin AutoCAD (build trên máy có AutoCAD 2021) |
| \`docs/\` | Hướng dẫn Admin/test |

## Chạy License Server

\`\`\`bash
cd LicenseServer
dotnet MepPanel.LicenseServer.dll
\`\`\`

Hoặc trên Windows: mở solution, F5 project \`MepPanel.LicenseServer\`.

Swagger mặc định: \`https://localhost:7024/swagger\`

- Admin key: xem \`LicenseServer/appsettings.json\` → \`Admin:ApiKey\`
- OTP test: \`123456\`
- User seed: \`0900000001\`, \`0900000002\`

Chi tiết: \`docs/LICENSE_ADMIN_GUIDE.md\`
EOF

echo "==> Tạo zip"
rm -f "$ZIP_SERVER" "$ZIP_FULL"
mkdir -p "$DIST"

(
  cd "$SERVER_OUT/.."
  # STAGE/LicenseServer -> zip server-only from parent of LicenseServer... 
  true
)

# Server-only zip
(
  cd "$STAGE"
  zip -qr "$ZIP_SERVER" LicenseServer RELEASE_NOTES.md VERSION CHANGELOG.md docs
)

# Full zip
(
  cd "$DIST/staging"
  zip -qr "$ZIP_FULL" "MepPanel-v$VERSION"
)

# checksums
(
  cd "$DIST"
  sha256sum "MepPanel-LicenseServer-v$VERSION.zip" "MepPanel-v$VERSION.zip" > "SHA256-v$VERSION.txt"
)

echo
echo "Đã đóng gói:"
echo "  - $ZIP_SERVER"
echo "  - $ZIP_FULL"
echo "  - $DIST/SHA256-v$VERSION.txt"
ls -lh "$ZIP_SERVER" "$ZIP_FULL"
