#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

VERSION="${1:-$(tr -d '[:space:]' < VERSION)}"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Version khong hop le: $VERSION"
  echo "Dung: ./scripts/pack-release.sh 0.3.0"
  exit 1
fi

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

DIST="$ROOT/dist"
STAGE="$DIST/staging/MepPanel-v$VERSION"
SERVER_OUT="$STAGE/LicenseServer"
BUNDLE_OUT="$STAGE/PluginBundle"
DOCS_OUT="$STAGE/docs"
DEPLOY_OUT="$STAGE/deploy"
PLUGIN_SRC_OUT="$STAGE/PluginSource"
CLIENT_OUT="$STAGE/ClientPlugin"
ZIP_SERVER="$DIST/MepPanel-LicenseServer-v$VERSION.zip"
ZIP_CLIENT="$DIST/MepPanel-Client-v$VERSION.zip"
ZIP_FULL="$DIST/MepPanel-v$VERSION.zip"

echo "==> Dong goi MepPanel v$VERSION"

rm -rf "$STAGE"
mkdir -p "$SERVER_OUT" "$BUNDLE_OUT" "$DOCS_OUT" "$DEPLOY_OUT" "$PLUGIN_SRC_OUT" "$CLIENT_OUT"

echo "==> Build LicenseServer (Release)"
dotnet publish "$ROOT/MepPanel.LicenseServer/MepPanel.LicenseServer.csproj" \
  -c Release \
  -o "$SERVER_OUT" \
  -p:MepPanelVersion="$VERSION" \
  -p:Version="$VERSION"

echo "==> Copy bundle + tai lieu + deploy"
cp -R "$ROOT/bundle/MepPanel.Plugin.bundle/." "$BUNDLE_OUT/"
cp "$ROOT/README.md" "$STAGE/"
cp "$ROOT/CHANGELOG.md" "$STAGE/"
cp "$ROOT/VERSION" "$STAGE/"
cp "$ROOT/docs/LICENSE_ADMIN_GUIDE.md" "$DOCS_OUT/"
cp "$ROOT/docs/PLUGIN_INSTALL.md" "$DOCS_OUT/"
cp "$ROOT/docs/DEPLOY_VPS.md" "$DOCS_OUT/"
cp "$ROOT/docs/REMOTE_DEPLOYMENT.md" "$DOCS_OUT/"
cp -R "$ROOT/deploy/." "$DEPLOY_OUT/"

echo "==> Tao goi ClientPlugin (tro Server trung tam)"
cp -R "$ROOT/bundle/MepPanel.Plugin.bundle" "$CLIENT_OUT/"
rm -f "$CLIENT_OUT/MepPanel.Plugin.bundle/Contents/MepPanel.config.json"
cp "$ROOT/scripts/install-client-plugin.ps1" "$CLIENT_OUT/Install-MepPanel-Client.ps1"

# Asset can co tren moi may client: thu vien AMC va renderer/Pillow assets.
CLIENT_CONTENTS="$CLIENT_OUT/MepPanel.Plugin.bundle/Contents"
mkdir -p "$CLIENT_CONTENTS/samples/templates" "$CLIENT_CONTENTS/devices"
cp "$ROOT/assets/templates/AMC_TEMPLATE_RV29.dwg" "$CLIENT_CONTENTS/samples/templates/"
cp "$ROOT/renderer/devices/"*.png "$CLIENT_CONTENTS/devices/"
for renderer in render_cabinet.py render_cabinet_interior.py render_photoreal.py render_pipe_system.py; do
  cp "$ROOT/renderer/$renderer" "$CLIENT_CONTENTS/"
done

echo "==> Copy plugin source (build tren Windows + AutoCAD 2021)"
for proj in MepPanel.AutoCAD MepPanel.AutoCAD.Licensing MepPanel.Blocks.AutoCAD MepPanel.Core; do
  cp -R "$ROOT/src/$proj/." "$PLUGIN_SRC_OUT/$proj/"
done

cat > "$STAGE/RELEASE_NOTES.md" <<EOF
# MepPanel v$VERSION

Ngay dong goi: $(date -u +%Y-%m-%d)

## Thanh phan trong goi

| Thu muc | Mo ta |
|---|---|
| \`LicenseServer/\` | May chu kiem soat (chay duoc) |
| \`PluginBundle/\` | Bundle AutoCAD (can build DLL tren Windows) |
| \`ClientPlugin/\` | Bundle da build + script cai cho may AutoCAD |
| \`PluginSource/\` | Source plugin (build tren may co AutoCAD 2021) |
| \`deploy/\` | Docker + nginx cho VPS |
| \`docs/\` | Huong dan Admin, cai plugin, deploy VPS |

## License Server

\`\`\`bash
cd LicenseServer
dotnet MepPanel.LicenseServer.dll
\`\`\`

Hoac VPS: xem \`docs/DEPLOY_VPS.md\`

- Admin: \`/admin\`
- Admin key: \`appsettings.json\` hoac env \`Admin__ApiKey\`
- OTP test (TestMode=true): \`123456\`

## Plugin AutoCAD

Windows PowerShell:

\`\`\`powershell
.\\scripts\\install-plugin-bundle.ps1
\`\`\`

May client: mo PowerShell Admin trong \`ClientPlugin/\` va chay:

\`\`\`powershell
.\\Install-MepPanel-Client.ps1 -LicenseServerUrl "https://license.congty.vn/"
\`\`\`

Mot License Server trung tam giu database va Admin; may client khong chay Server.
Xem \`docs/REMOTE_DEPLOYMENT.md\`.

Lenh: \`MEPSTATUS\`, \`MEPLOGIN\`, \`MEPDB\`, \`MEPHVAC\`, \`MEPLOGOUT\`
EOF

echo "==> Tao zip"
rm -f "$ZIP_SERVER" "$ZIP_CLIENT" "$ZIP_FULL"
mkdir -p "$DIST"

(
  cd "$STAGE"
  zip -qr "$ZIP_SERVER" LicenseServer RELEASE_NOTES.md VERSION CHANGELOG.md docs deploy
)

(
  cd "$STAGE"
  zip -qr "$ZIP_CLIENT" ClientPlugin RELEASE_NOTES.md docs/REMOTE_DEPLOYMENT.md
)

(
  cd "$DIST/staging"
  zip -qr "$ZIP_FULL" "MepPanel-v$VERSION"
)

(
  cd "$DIST"
  sha256sum "MepPanel-LicenseServer-v$VERSION.zip" "MepPanel-Client-v$VERSION.zip" "MepPanel-v$VERSION.zip" > "SHA256-v$VERSION.txt"
)

echo
echo "Da dong goi:"
echo "  - $ZIP_SERVER"
echo "  - $ZIP_CLIENT"
echo "  - $ZIP_FULL"
echo "  - $DIST/SHA256-v$VERSION.txt"
ls -lh "$ZIP_SERVER" "$ZIP_CLIENT" "$ZIP_FULL"
