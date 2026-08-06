#!/usr/bin/env python3
"""
MEP Cabinet Combined Renderer — PIL layout + AI enhance.
Pipeline: JSON/CSV → PIL (layout chính xác) → AI GenerateImage (photorealistic)

Usage:
    python3 render_combined.py --input TD-01.json --output cabinet.png
    python3 render_combined.py --demo --output cabinet.png

Yêu cầu: Pillow + cursor AI API (chạy trong Cursor IDE)
"""

import argparse, os, sys, subprocess, json, tempfile

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))


def step1_pil_layout(input_path: str, base_output: str) -> str:
    """Bước 1: Vẽ layout chính xác bằng PIL v4."""
    renderer = os.path.join(SCRIPT_DIR, "render_cabinet_v4.py")
    if not os.path.exists(renderer):
        print("ERROR: render_cabinet_v4.py not found")
        sys.exit(1)

    args = ["python3", renderer, "--output", base_output]
    if input_path:
        args += ["--input", input_path]
    else:
        args += ["--demo"]

    print(f"[Step 1] PIL layout render: {renderer}")
    result = subprocess.run(args, capture_output=True, text=True)
    if result.returncode != 0:
        print("PIL render error:", result.stderr)
        sys.exit(1)
    print(f"         {result.stdout.strip()}")
    return base_output


def step2_generate_prompt(input_path: str) -> str:
    """Bước 2: Tạo prompt chi tiết từ dữ liệu."""
    prompter = os.path.join(SCRIPT_DIR, "generate_cabinet_prompt.py")
    if not os.path.exists(prompter):
        return None

    args = ["python3", prompter]
    if input_path:
        args += ["--input", input_path]
    else:
        args += ["--demo"]
    args += ["--output", "-"]

    result = subprocess.run(args, capture_output=True, text=True)
    if result.returncode != 0:
        return None
    return result.stdout.strip()


def step3_ai_enhance(base_png: str, prompt: str, output_path: str):
    """
    Bước 3: AI enhance (gọi từ Cursor IDE hoặc API).
    Trong môi trường Cursor, GenerateImage được gọi tự động.
    Script này in ra thông tin để dùng thủ công hoặc API call.
    """
    print(f"\n[Step 3] AI Enhancement")
    print(f"         Base layout: {base_png}")
    print(f"         Output: {output_path}")
    print(f"         Prompt length: {len(prompt)} chars")
    print()
    print("=" * 60)
    print("PROMPT (dùng với AI Image Generation API):")
    print("=" * 60)
    print(prompt[:500] + "..." if len(prompt) > 500 else prompt)
    print("=" * 60)
    print()
    print(f"Reference image: {base_png}")
    print()
    print("Để generate AI trong Cursor, dùng GenerateImage tool với:")
    print(f"  reference_image_paths: [\"{base_png}\"]")
    print(f"  description: <prompt trên>")


def main():
    p = argparse.ArgumentParser(description="MEP Cabinet Combined Renderer (PIL + AI)")
    p.add_argument("--input", "-i", help="JSON hoặc CSV khối lượng")
    p.add_argument("--output", "-o", default="cabinet_combined.png")
    p.add_argument("--demo", action="store_true")
    p.add_argument("--pil-only", action="store_true", help="Chỉ render PIL, không AI")
    args = p.parse_args()

    input_path = args.input if not args.demo else None
    base_png = args.output.replace(".png", "_base.png")

    # Step 1: PIL layout
    step1_pil_layout(input_path, base_png)

    if args.pil_only:
        import shutil
        shutil.copy(base_png, args.output)
        print(f"\nPIL-only output: {args.output}")
        return

    # Step 2: Prompt
    prompt = step2_generate_prompt(input_path)
    if not prompt:
        import shutil
        shutil.copy(base_png, args.output)
        print("Prompt generation failed, using PIL output only")
        return

    # Step 3: AI enhance info
    step3_ai_enhance(base_png, prompt, args.output)

    print("\n[Pipeline] Hoàn tất!")
    print(f"  Base (PIL)  : {base_png}")
    print(f"  Final (AI)  : Gọi GenerateImage với reference_image_paths trên")
    print()
    print("Trong AutoCAD plugin:")
    print("  MEPDB → Tủ điện / DB → Render bố trí tủ → PNG → chọn Combined")


if __name__ == "__main__":
    main()
