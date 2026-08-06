#!/usr/bin/env bash
# =============================================================================
# pack-release.sh — Đóng gói MEP Drawing Tool thành bộ ZIP hoàn chỉnh
#
# Cấu trúc output:
#   MEP-Drawing-Tool-v{VERSION}/
#   ├── 1_plugin/          C# source + build scripts + bundle
#   ├── 2_renderer/        Python renderer + device images + samples
#   ├── 3_license_server/  Server pre-built (dotnet publish)
#   ├── 4_samples/         DWG template + cabinet data + renders
#   └── docs/              Tất cả tài liệu
#
# Cách dùng:
#   ./scripts/pack-release.sh 0.16.0
#   ./scripts/pack-release.sh          (dùng VERSION file)
# =============================================================================
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${1:-$(tr -d '[:space:]' < "$ROOT/VERSION")}"

if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "VERSION khong hop le: '$VERSION'"
  echo "Dung: ./scripts/pack-release.sh 0.16.0"
  exit 1
fi

DIST="$ROOT/dist"
PKG="MEP-Drawing-Tool-v$VERSION"
STAGE="$DIST/staging/$PKG"
ZIP_OUT="$DIST/$PKG.zip"
SHA_OUT="$DIST/SHA256-v$VERSION.txt"

echo "============================================"
echo "  MEP DRAWING TOOL — Pack v$VERSION"
echo "============================================"

# ── Clean ──────────────────────────────────────────────────────────────────
rm -rf "$STAGE"
mkdir -p \
  "$STAGE/1_plugin/src" \
  "$STAGE/1_plugin/bundle" \
  "$STAGE/1_plugin/scripts" \
  "$STAGE/2_renderer/devices" \
  "$STAGE/2_renderer/samples" \
  "$STAGE/3_license_server" \
  "$STAGE/4_samples/templates" \
  "$STAGE/4_samples/cabinet/devices" \
  "$STAGE/docs"

# ── Root files ─────────────────────────────────────────────────────────────
cp "$ROOT/README.md"   "$STAGE/"
cp "$ROOT/VERSION"     "$STAGE/"
[[ -f "$ROOT/CHANGELOG.md" ]] && cp "$ROOT/CHANGELOG.md" "$STAGE/"

# ── 1. Plugin (C# source + bundle + scripts) ───────────────────────────────
echo "--> 1_plugin"

# Source projects (C# — cần Windows để build)
for proj in MepPanel.AutoCAD MepPanel.AutoCAD.Licensing MepPanel.Blocks.AutoCAD MepPanel.Core; do
  src="$ROOT/src/$proj"
  if [[ -d "$src" ]]; then
    dst="$STAGE/1_plugin/src/$proj"
    mkdir -p "$dst"
    cp -r "$src/." "$dst/"
    rm -rf "$dst/bin" "$dst/obj" 2>/dev/null || true
  fi
done

# Solution file
cp "$ROOT/MepPanelMvp.sln" "$STAGE/1_plugin/" 2>/dev/null || true

# Plugin bundle (skip PDB)
cp -r "$ROOT/bundle/MepPanel.Plugin.bundle/." "$STAGE/1_plugin/bundle/MepPanel.Plugin.bundle/"
find "$STAGE/1_plugin/bundle" -name "*.pdb" -delete 2>/dev/null || true

# Build + install scripts
cp "$ROOT/scripts/build-plugin-from-repo.ps1"  "$STAGE/1_plugin/scripts/"
cp "$ROOT/scripts/install-plugin-bundle.ps1"   "$STAGE/1_plugin/scripts/"
[[ -f "$ROOT/scripts/build-plugin-release.ps1" ]] && \
  cp "$ROOT/scripts/build-plugin-release.ps1" "$STAGE/1_plugin/scripts/"
[[ -f "$ROOT/plugin.local.json.example" ]] && \
  cp "$ROOT/plugin.local.json.example" "$STAGE/1_plugin/"

# Patches dir
if [[ -d "$ROOT/patches" ]]; then
  cp -r "$ROOT/patches/" "$STAGE/1_plugin/patches/"
fi

# ── 2. Python Renderer ─────────────────────────────────────────────────────
echo "--> 2_renderer"

cp "$ROOT/scripts/render_cabinet_v3.py" "$STAGE/2_renderer/render_cabinet.py"

# Device images
if [[ -d "$ROOT/samples/cabinet/devices" ]]; then
  cp "$ROOT/samples/cabinet/devices/"*.png "$STAGE/2_renderer/devices/" 2>/dev/null || true
fi

# Sample data
cp "$ROOT/samples/cabinet/TD-01_full.json" "$STAGE/2_renderer/samples/"
cp "$ROOT/samples/cabinet/TD-01_full.csv"  "$STAGE/2_renderer/samples/"

# requirements.txt
cat > "$STAGE/2_renderer/requirements.txt" <<'EOF'
Pillow>=10.0.0
EOF

# README renderer
cat > "$STAGE/2_renderer/README_RENDERER.md" <<'EOF'
# MEP Cabinet Renderer

## Cài đặt
```bash
pip install -r requirements.txt
```

## Sử dụng

```bash
# Demo
python3 render_cabinet.py --demo --output cabinet.png

# Từ CSV khối lượng
python3 render_cabinet.py --input samples/TD-01_full.csv --output cabinet.png

# Từ JSON đầy đủ
python3 render_cabinet.py --input samples/TD-01_full.json --output cabinet.png
```

## Thêm ảnh thiết bị

Đặt file PNG vào thư mục `devices/` theo tên:
- `device_mccb_schneider.png`
- `device_mcb1p_schneider.png`
- `device_mcb3p_schneider.png`
- `device_contactor_ls.png`
- `device_relay_ls.png`
- `device_timer_schneider.png`
- `device_meter_pm5560.png`

## Format CSV tối thiểu

```csv
bay,ten_thiet_bi,device_type,in_a,poles,qty,manufacturer
NGĂN 1,MCCB tổng,MCCB,100,3,1,Schneider
NGĂN 2,MCB đèn,MCB 1P,10,1,4,Schneider
NGĂN 3,Contactor,CONTACTOR,25,3,2,LS
```
EOF

# ── 3. License Server (pre-built) ──────────────────────────────────────────
echo "--> 3_license_server"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

if dotnet --version &>/dev/null; then
  dotnet publish "$ROOT/MepPanel.LicenseServer/MepPanel.LicenseServer.csproj" \
    -c Release -o "$STAGE/3_license_server" \
    -p:Version="$VERSION" \
    --self-contained false \
    -r win-x64 \
    2>&1 | tail -5 || \
  dotnet publish "$ROOT/MepPanel.LicenseServer/MepPanel.LicenseServer.csproj" \
    -c Release -o "$STAGE/3_license_server" \
    -p:Version="$VERSION" \
    2>&1 | tail -5
  echo "   License Server built OK"
else
  echo "   SKIP: dotnet not found, copy source instead"
  mkdir -p "$STAGE/3_license_server/source"
  cp -r "$ROOT/MepPanel.LicenseServer/." "$STAGE/3_license_server/source/"
  rm -rf "$STAGE/3_license_server/source/bin" "$STAGE/3_license_server/source/obj" 2>/dev/null || true
fi

# Start script
cat > "$STAGE/3_license_server/START_SERVER.ps1" <<'EOF'
# Khoi dong License Server
# Lan dau: chinh AdminKey trong appsettings.Production.json
$env:ASPNETCORE_URLS = "http://localhost:5268;https://localhost:7024"
dotnet MepPanel.LicenseServer.dll
EOF

cat > "$STAGE/3_license_server/START_SERVER.sh" <<'EOF'
#!/bin/bash
export ASPNETCORE_URLS="http://localhost:5268;https://localhost:7024"
dotnet MepPanel.LicenseServer.dll
EOF
chmod +x "$STAGE/3_license_server/START_SERVER.sh"

# Deploy scripts (Docker nếu có)
if [[ -d "$ROOT/deploy" ]]; then
  cp -r "$ROOT/deploy/" "$STAGE/3_license_server/deploy/"
fi

# ── 4. Samples ─────────────────────────────────────────────────────────────
echo "--> 4_samples"

# DWG template
[[ -f "$ROOT/samples/templates/AMC_TEMPLATE_RV29.dwg" ]] && \
  cp "$ROOT/samples/templates/AMC_TEMPLATE_RV29.dwg" "$STAGE/4_samples/templates/"

# Cabinet data + renders
cp "$ROOT/samples/cabinet/TD-01_full.json"         "$STAGE/4_samples/cabinet/"
cp "$ROOT/samples/cabinet/TD-01_full.csv"           "$STAGE/4_samples/cabinet/"
cp "$ROOT/samples/cabinet/"*.png 2>/dev/null        "$STAGE/4_samples/cabinet/" || true
cp "$ROOT/samples/cabinet/devices/"*.png 2>/dev/null "$STAGE/4_samples/cabinet/devices/" || true

# ── Docs ───────────────────────────────────────────────────────────────────
echo "--> docs"
for f in "$ROOT/docs/"*.md; do
  cp "$f" "$STAGE/docs/"
done

# ── RELEASE NOTES ──────────────────────────────────────────────────────────
cat > "$STAGE/RELEASE_NOTES.md" <<EOF
# MEP Drawing Tool v$VERSION

**Ngày đóng gói:** $(date -u +"%Y-%m-%d %H:%M UTC")
**Branch:** cursor/plugin-features-cc24

---

## Nội dung gói

| Thư mục | Nội dung |
|---------|----------|
| \`1_plugin/\` | Plugin AutoCAD (C# source + bundle + scripts build) |
| \`2_renderer/\` | Python renderer tủ điện (ảnh thiết bị + samples) |
| \`3_license_server/\` | License Server pre-built (chạy ngay) |
| \`4_samples/\` | DWG template AMC + dữ liệu tủ mẫu + ảnh render |
| \`docs/\` | 14 tài liệu kỹ thuật |

---

## Bắt đầu nhanh

### Plugin AutoCAD
\`\`\`powershell
cd 1_plugin
# Đóng AutoCAD, rồi:
.\\scripts\\build-plugin-from-repo.ps1
\`\`\`

### Render tủ điện
\`\`\`bash
cd 2_renderer
pip install -r requirements.txt
python3 render_cabinet.py --input samples/TD-01_full.csv --output cabinet.png
\`\`\`

### License Server
\`\`\`powershell
cd 3_license_server
.\\START_SERVER.ps1
# Admin: http://localhost:5268/admin
# OTP test: 123456
\`\`\`

---

## Tính năng v$VERSION

- Panel MEP DRAWING TOOL (7 nhóm chức năng)
- Máng cáp, trunking hệ điện
- HVAC: ống mềm, co 45°/90°, reducer tự động
- Hệ nước + PCCC: ống đa điểm + 11 loại phụ kiện AMC
- Tiêu chuẩn TCVN/QCVN/NFPA/ASHRAE + tính toán 4 hệ
- Render tủ điện photorealistic từ CSV/JSON (7 thiết bị Schneider/LS)
- License Server + Admin web

---

## Nâng cấp lên phiên bản mới

\`\`\`powershell
git pull origin cursor/plugin-features-cc24
.\\1_plugin\\scripts\\build-plugin-from-repo.ps1
\`\`\`

Hoặc tải ZIP mới từ releases và chạy lại \`build-plugin-from-repo.ps1\`.

---

_Bản quyền © MEP Drawing Tool. Xem docs/COMMERCIAL_ROADMAP.md._
EOF

# ── Tạo ZIP ────────────────────────────────────────────────────────────────
echo "--> Tao ZIP"
rm -f "$ZIP_OUT"
mkdir -p "$DIST"

(
  cd "$DIST/staging"
  zip -qr "$ZIP_OUT" "$PKG"
)

# SHA256
(
  cd "$DIST"
  sha256sum "$PKG.zip" > "$SHA_OUT" 2>/dev/null || \
  openssl dgst -sha256 "$PKG.zip" > "$SHA_OUT" 2>/dev/null || true
)

# ── Summary ────────────────────────────────────────────────────────────────
SIZE=$(du -sh "$ZIP_OUT" | cut -f1)
echo
echo "============================================"
echo "  DONE! v$VERSION"
echo "============================================"
echo "  ZIP  : $ZIP_OUT ($SIZE)"
echo "  SHA  : $SHA_OUT"
echo
echo "  Noi dung:"
find "$STAGE" -maxdepth 2 -type d | sort | sed "s|$STAGE||" | grep -v "^$"
