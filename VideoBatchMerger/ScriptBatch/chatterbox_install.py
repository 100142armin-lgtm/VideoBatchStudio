"""Install the pinned official Chatterbox runtime. No server or user media upload."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import urllib.request
import zipfile

CODE_REVISION = "5de7a54aa4e5e2baadb0182dde554908b48b85c2"
MODEL_REVISION = "5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18"
PERTH_VERSION = "1.0.1"
FILES = {
    "ve.pt": "4b16d836bc598509860f6fa068165a8bb5e9ac84f05582dfcf278a5a372879f1",
    "s3gen.pt": "9b9ff07e60b20c136e2b1b3d7563a24604e8d2c4c267888d1ee929dd0151d2a3",
    "t3_mtl23ls_v3.safetensors": "5abca8321ede76f8e61f1cc0d19aea6c946b28871017ce8726f8a69203f05953",
    "conds.pt": "6552d70568833628ba019c6b03459e77fe71ca197d5c560cef9411bee9d87f4e",
    "grapheme_mtl_merged_expanded_v1.json": "69632f47220a788a52ce2661d096453c5655e9bf25289d89a8d832c46ee07dbf",
    "Cangjie5_TC.json": "7073fd9de919443ae88e0bd2449917a65fe54898a4413ed1edcc4b67f28bce8c",
}


def log(message):
    print(message, flush=True)


def digest(path):
    with open(path, "rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def download(url, path, sha=None):
    if path.exists() and (not sha or digest(path) == sha):
        return
    log("下载 " + path.name)
    partial = path.with_suffix(path.suffix + ".part")
    for attempt in range(4):
        try:
            existing = partial.stat().st_size if partial.exists() else 0
            request = urllib.request.Request(url, headers={"Range": f"bytes={existing}-"} if existing else {})
            with urllib.request.urlopen(request, timeout=120) as response:
                append = existing and response.status == 206
                count = existing if append else 0
                next_report = count + 128 * 1024 * 1024
                with open(partial, "ab" if append else "wb") as output:
                    while data := response.read(1024 * 1024):
                        output.write(data)
                        count += len(data)
                        if count >= next_report:
                            log(f"{path.name}: {count // (1024 * 1024)} MB")
                            next_report += 128 * 1024 * 1024
            break
        except Exception:
            if attempt == 3:
                raise
            time.sleep(2)
    if sha and digest(partial) != sha:
        raise RuntimeError("文件校验失败: " + path.name)
    os.replace(partial, path)


def pip(*args):
    subprocess.run([sys.executable, "-m", "pip", "--isolated", "--disable-pip-version-check",
                    "--retries", "10", "--timeout", "120", *args], check=True)


def select_backend(device="auto"):
    if device in ("cpu", "cuda"):
        return device
    try:
        result = subprocess.run(["nvidia-smi", "--query-gpu=name", "--format=csv,noheader"],
                                capture_output=True, text=True, timeout=10,
                                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        return "cuda" if result.returncode == 0 and result.stdout.strip() else "cpu"
    except (OSError, subprocess.SubprocessError):
        return "cpu"


def check_installation(root):
    if not (root / "ready.json").is_file():
        raise RuntimeError("缺少安装完成标记，请继续安装")
    for name, sha in FILES.items():
        if not (root / "models" / name).is_file() or digest(root / "models" / name) != sha:
            raise RuntimeError("模型缺失或校验失败: " + name)
    import whisper
    expected = whisper._MODELS["small"].split("/")[-2]
    alignment = root / "alignment" / "small.pt"
    if not alignment.is_file() or digest(alignment) != expected:
        raise RuntimeError("逐词对齐模型缺失或损坏")
    import torch
    import chatterbox.mtl_tts
    import stable_whisper
    import perth
    log("Chatterbox 组件和模型校验通过；运行设备: " + ("NVIDIA GPU" if torch.cuda.is_available() else "CPU"))


def install(root, device="auto"):
    root.mkdir(parents=True, exist_ok=True)
    (root / "ready.json").unlink(missing_ok=True)
    os.environ["HF_HOME"] = str(root / "cache" / "huggingface")
    os.environ["NUMBA_CACHE_DIR"] = str(root / "cache" / "numba")
    # Keep the local speech stack isolated from Edge-TTS. CPU computers need no CUDA wheels.
    backend = select_backend(device)
    variant = "cu128" if backend == "cuda" else "cpu"
    torch_version = "2.11.0+" + variant
    constraints = root / "runtime-constraints.txt"
    constraints.write_text("\n".join(line for line in Path(__file__).with_name("chatterbox-requirements.lock").read_text().splitlines()
                                     if not line.startswith(("torch==", "torchaudio=="))), encoding="utf-8")
    log("安装独立 " + ("NVIDIA GPU" if backend == "cuda" else "CPU") + " 运行库（首次需要下载数 GB）")
    pip("install", "--no-cache-dir", "torch==" + torch_version, "torchaudio==" + torch_version,
        "--index-url", "https://download.pytorch.org/whl/" + variant)
    pip("install", "--no-cache-dir", "--index-url", "https://pypi.org/simple",
        "--constraint", str(constraints),
        "numpy==1.26.4", "librosa==0.11.0", "s3tokenizer==0.3.0",
        "torch==" + torch_version, "torchaudio==" + torch_version,
        "transformers==5.10.0", "diffusers==0.38.0", "setuptools==81.0.0", "conformer==0.3.2", "safetensors==0.8.0",
        "pykakasi==2.3.0", "pyloudnorm==0.1.1", "omegaconf==2.3.0", "spacy-pkuseg==1.0.1",
        "stable-ts==2.19.1", "openai-whisper==20250625", "pydub==0.25.1")
    for name, revision, checksum in [
        ("chatterbox", CODE_REVISION, "bb7363a76c0c0432a0305681fe2e533bbfb0f10fcf7b98d75a439d9a909f1576"),
    ]:
        repo = "chatterbox" if name == "chatterbox" else "Perth"
        archive = root / (name + ".zip")
        download(f"https://codeload.github.com/resemble-ai/{repo}/zip/{revision}", archive, checksum)
        pip("install", "--no-cache-dir", "--no-build-isolation", "--no-deps", str(archive))
    perth_wheel = root / "resemble_perth-1.0.1-py3-none-any.whl"
    download("https://files.pythonhosted.org/packages/77/cc/73226dd776f8e9c2975f64f4efc22988fb37e5b185ba5cccd6f2e7196954/resemble_perth-1.0.1-py3-none-any.whl",
             perth_wheel, "65e9c37531b1a128a4a56226b75dece4521683cf3611b0b2a5ffe234f00c9342")
    pip("install", "--no-cache-dir", "--no-deps", str(perth_wheel))
    log("下载并验证官方多语模型")
    models = root / "models"
    models.mkdir(exist_ok=True)
    for name, sha in FILES.items():
        download(f"https://huggingface.co/ResembleAI/chatterbox/resolve/{MODEL_REVISION}/{name}", models / name, sha)
    log("准备本地逐词对齐模型")
    import whisper
    alignment = root / "alignment"
    alignment.mkdir(exist_ok=True)
    alignment_url = whisper._MODELS["small"]
    download(alignment_url, alignment / "small.pt", alignment_url.split("/")[-2])
    whisper.load_model(str(alignment / "small.pt"), device="cpu")
    notices = root.parent / "licenses" / "Chatterbox"
    notices.mkdir(parents=True, exist_ok=True)
    for name, archive in [("Chatterbox-MIT.txt", root / "chatterbox.zip"), ("Perth-LICENSE.txt", perth_wheel)]:
        with zipfile.ZipFile(archive) as zf:
            member = next(n for n in zf.namelist() if n.endswith("/LICENSE") or n.endswith("/LICENSE.txt"))
            (notices / name).write_bytes(zf.read(member))
    # Keep distribution metadata, including licenses of transitive Python packages.
    packages = subprocess.check_output([sys.executable, "-m", "pip", "freeze"], text=True)
    (notices / "installed-packages.txt").write_text(packages, encoding="utf-8")
    manifest = {"schema": 1, "code_revision": CODE_REVISION, "model_revision": MODEL_REVISION,
                "files": {n: digest(models / n) for n in FILES}, "watermark": "PerTh enabled",
                "alignment": "Whisper small / stable-ts", "backend": backend}
    (root / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    # The marker is written only after imports and all model downloads succeed.
    import chatterbox.mtl_tts
    import stable_whisper
    log("安装完成：配音和字幕对齐均可离线运行")
    (root / "ready.json").write_text(json.dumps(manifest), encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--device", choices=["auto", "cpu", "cuda"], default="auto")
    parser.add_argument("--check-only", action="store_true")
    args = parser.parse_args()
    try:
        if args.check_only:
            check_installation(Path(args.root).resolve())
        else:
            install(Path(args.root).resolve(), args.device)
    except Exception as exc:
        log("安装失败: " + str(exc))
        sys.exit(1)
