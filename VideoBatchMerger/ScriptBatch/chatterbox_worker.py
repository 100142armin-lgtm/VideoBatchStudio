"""Private stdin/stdout worker. Models stay local; stdout is JSON messages only."""
import argparse
import contextlib
import difflib
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import sys
import time
import traceback
import wave

os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["HF_HUB_DISABLE_TELEMETRY"] = "1"
os.environ["DO_NOT_TRACK"] = "1"
os.environ["TOKENIZERS_PARALLELISM"] = "false"
os.environ["TORCH_FORCE_WEIGHTS_ONLY_LOAD"] = "1"


def _deny_network(*args, **kwargs):
    raise OSError("Chatterbox inference is offline: network connections are disabled")


# Installation downloads run in a separate process. Inference cannot connect out.
socket.socket.connect = _deny_network
socket.socket.connect_ex = _deny_network
socket.create_connection = _deny_network

_tts = None
_aligner = None
_default_conds = None
_device = None
_verified = False


def emit(message):
    print(json.dumps(message, ensure_ascii=False), flush=True)


def split_script(text, maximum=220):
    """Keep sentences intact when possible; bound each generation below its token limit."""
    parts = re.split(r"(?<=[.!?;。！？；])\s+|[\r\n]+", text.strip())
    chunks = []
    current = ""
    for part in parts:
        for token in part.split():
            if len(token) > maximum:
                raise ValueError("文案包含过长的单词，请先检查文案。")
            candidate = (current + " " + token).strip()
            if len(candidate) > maximum:
                chunks.append(current)
                current = token
            else:
                current = candidate
        if current:
            chunks.append(current)
            current = ""
    if not chunks:
        raise ValueError("文案为空")
    return chunks


def normalize(value):
    return "".join(c.lower() for c in value if c.isalnum())


def map_aligned_words(text, aligned, duration):
    """Restore exact script punctuation, using actual forced-alignment word boundaries."""
    source = []
    cursor = 0
    for word in aligned:
        value = normalize(word["word"])
        if not value:
            continue
        source.append((cursor, cursor + len(value), float(word["start"]), float(word["end"])))
        cursor += len(value)
    if normalize(text) != "".join(normalize(w["word"]) for w in aligned):
        raise ValueError("逐词对齐结果与文案不符，已停止生成以避免错误高亮。")
    words = []
    cursor = 0
    for token in text.split():
        count = len(normalize(token))
        if not count:
            if words:
                words[-1]["text"] += token
            continue
        matches = [w for w in source if w[0] < cursor + count and w[1] > cursor]
        if not matches:
            raise ValueError("未找到单词时间点: " + token)
        start = max(0.0, matches[0][2])
        end = min(duration, matches[-1][3])
        if end <= start:
            raise ValueError("有单词未能可靠对齐，请降低情绪强度后重试: " + token)
        words.append({"text": token, "start": round(start, 4), "end": round(end, 4)})
        cursor += count
    return words


def srt_time(value):
    milliseconds = max(0, round(value * 1000))
    seconds, ms = divmod(milliseconds, 1000)
    minutes, sec = divmod(seconds, 60)
    hours, minute = divmod(minutes, 60)
    return f"{hours:02}:{minute:02}:{sec:02},{ms:03}"


def write_subtitles(words, destination):
    chunks = []
    current = []
    for i, word in enumerate(words):
        current.append(word)
        gap = i + 1 < len(words) and words[i + 1]["start"] - word["end"] > 0.38
        if len(current) >= 4 or sum(len(w["text"]) for w in current) >= 24 or re.search(r"[.!?,;:。！？]$", word["text"]) or gap:
            chunks.append(current)
            current = []
    if current:
        chunks.append(current)
    lines = []
    for i, chunk in enumerate(chunks):
        end = chunk[-1]["end"]
        if i + 1 < len(chunks) and 0 < chunks[i + 1][0]["start"] - end < 0.35:
            end = max(end, chunks[i + 1][0]["start"] - 0.02)
        lines.extend([str(i + 1), f'{srt_time(chunk[0]["start"])} --> {srt_time(end)}', " ".join(w["text"] for w in chunk), ""])
    Path(destination).write_text("\n".join(lines), encoding="utf-8")
    Path(str(destination) + ".words.json").write_text(json.dumps(words, ensure_ascii=False), encoding="utf-8")


def recognized_word_fallback(audio, text, language, duration):
    """Use acoustic ASR word times if forced alignment collapses a short inflected word.

    Only accept equal word counts and exact / very close lexical matches. Never
    distribute a sentence uniformly or silently accept omitted words.
    """
    recognized = _aligner.transcribe(str(audio), language=language, regroup=False,
                                     verbose=None, word_timestamps=True)
    actual = [word for segment in recognized.to_dict()["segments"] for word in segment["words"]
              if normalize(word["word"])]
    tokens = [token for token in text.split() if normalize(token)]
    if len(tokens) != len(actual):
        raise ValueError("识别结果的词数与文案不同")
    mapped = []
    for target, word in zip(tokens, actual):
        left, right = normalize(target), normalize(word["word"])
        if left != right and difflib.SequenceMatcher(None, left, right).ratio() < .75:
            raise ValueError("识别结果与文案不符: " + target)
        mapped.append({**word, "word": target})
    return map_aligned_words(text, mapped, duration)


def load_models(root, progress, require_alignment=True):
    global _tts, _default_conds, _device, _aligner, _verified
    if not _verified:
        progress("正在校验本地模型文件")
        import importlib.util
        spec = importlib.util.spec_from_file_location("chatterbox_install", Path(__file__).with_name("chatterbox_install.py"))
        installer = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(installer)
        for name, expected in installer.FILES.items():
            if installer.digest(root / "models" / name) != expected:
                raise ValueError("模型文件校验失败，请重新安装: " + name)
        _verified = True
    import torch
    torch.set_num_threads(4)
    if _tts is None:
        progress("正在加载本地 Chatterbox 模型（首次加载较慢）")
        from chatterbox.mtl_tts import ChatterboxMultilingualTTS
        from chatterbox.models.tokenizers import tokenizer as tokenizer_module
        original_converter = tokenizer_module.ChineseCangjieConverter

        class LazyCangjieConverter:
            # Upstream eagerly downloads a Chinese segmenter even for Portuguese.
            # Defer that optional preprocessor; our UI offers only local Western-language inference.
            def __init__(self, *args, **kwargs):
                self.args, self.kwargs, self.instance = args, kwargs, None

            def __call__(self, value):
                if self.instance is None:
                    self.instance = original_converter(*self.args, **self.kwargs)
                return self.instance(value)

        _device = "cuda" if torch.cuda.is_available() else "cpu"
        tokenizer_module.ChineseCangjieConverter = LazyCangjieConverter
        try:
            _tts = ChatterboxMultilingualTTS.from_local(root / "models", device=_device, t3_model="v3")
        finally:
            tokenizer_module.ChineseCangjieConverter = original_converter
        _default_conds = _tts.conds
    if require_alignment and _aligner is None:
        progress("正在加载本地逐词字幕对齐模型")
        import stable_whisper
        _aligner = stable_whisper.load_model("small", device=_device, download_root=str(root / "alignment"))


def generate_aligned_part(request, chunk, number, count, output, reference, progress):
    import numpy as np
    import torch
    temporary = output.parent / (output.stem + f"_part{number}.wav")
    rate = min(1.5, max(0.75, float(request.get("rate", 1.0))))
    last_error = None
    for attempt in range(3):
        progress(f"本地配音 {number + 1}/{count}" + (f"（对齐重试 {attempt}）" if attempt else ""))
        torch.manual_seed(int(request.get("seed", 42)) + number * 101 + attempt)
        with torch.inference_mode():
            wav = _tts.generate(chunk, language_id=request.get("language", "pt"),
                                audio_prompt_path=reference if number == 0 and attempt == 0 and reference else None,
                                exaggeration=min(1.5, max(0.0, float(request.get("exaggeration", .65)))),
                                cfg_weight=min(1.0, max(0.0, float(request.get("cfg", .35))))).squeeze().cpu().numpy()
        with wave.open(str(temporary), "wb") as stream:
            stream.setnchannels(1)
            stream.setsampwidth(2)
            stream.setframerate(_tts.sr)
            stream.writeframes((np.clip(wav, -1, 1) * 32767).astype("<i2").tobytes())
        actual_path = temporary
        if abs(rate - 1.0) > .001:
            actual_path = temporary.with_name(temporary.stem + "_rate.wav")
            subprocess.run([request["ffmpeg"], "-y", "-v", "error", "-i", str(temporary),
                            "-af", f"atempo={rate}", "-ar", str(_tts.sr), "-ac", "1", str(actual_path)],
                           check=True, capture_output=True, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        with wave.open(str(actual_path), "rb") as stream:
            actual = np.frombuffer(stream.readframes(stream.getnframes()), dtype="<i2").astype(np.float32) / 32768
        duration = len(actual) / _tts.sr
        if request.get("audio_only", False):
            temporary.unlink(missing_ok=True)
            if actual_path != temporary:
                actual_path.unlink(missing_ok=True)
            return actual, []
        progress(f"正在按实际音频对齐字幕 {number + 1}/{count}")
        result = _aligner.align(str(actual_path), chunk, language=request.get("language", "pt"), regroup=False,
                                verbose=None, suppress_silence=True, remove_instant_words=False)
        if result is not None:
            aligned = [w for segment in result.to_dict()["segments"] for w in segment["words"]]
            if any(w["end"] <= w["start"] for w in aligned):
                progress("正在精修短词时间点")
                result = _aligner.refine(str(actual_path), result, precision=.02, prob_threshold=.1, verbose=None)
                aligned = [w for segment in result.to_dict()["segments"] for w in segment["words"]]
            try:
                words = map_aligned_words(chunk, aligned, duration)
                temporary.unlink(missing_ok=True)
                if actual_path != temporary:
                    actual_path.unlink(missing_ok=True)
                return actual, words
            except ValueError as exc:
                last_error = exc
                progress("正在复核短词的实际发音时间")
                try:
                    words = recognized_word_fallback(actual_path, chunk, request.get("language", "pt"), duration)
                    temporary.unlink(missing_ok=True)
                    if actual_path != temporary:
                        actual_path.unlink(missing_ok=True)
                    return actual, words
                except ValueError:
                    pass
        else:
            last_error = ValueError("音频无法与文案对齐，请换参考声音后重试。")
    raise last_error


def synthesize(request, root, progress):
    started = time.perf_counter()
    import numpy as np
    if request.get("language", "pt") not in {"pt", "es", "en", "fr", "de", "it"}:
        raise ValueError("本地配音目前支持葡语、西语、英语、法语、德语和意大利语。")
    audio_only = bool(request.get("audio_only", False))
    load_models(root, progress, require_alignment=not audio_only)
    loaded = time.perf_counter()
    text = request["text"].strip()
    if len(text) > 30000:
        raise ValueError("单篇文案过长，请分篇生成。")
    language = request.get("language", "pt")
    if language not in _tts.get_supported_languages():
        raise ValueError("不支持的配音语言")
    reference = request.get("reference", "")
    if reference and not Path(reference).is_file():
        raise ValueError("参考声音文件不存在，请重新选择。")
    output = Path(request["audio"])
    os.environ["PATH"] = str(Path(request["ffmpeg"]).parent) + os.pathsep + os.environ.get("PATH", "")
    output.parent.mkdir(parents=True, exist_ok=True)
    chunks = split_script(text)
    _tts.conds = _default_conds
    audio_parts = []
    words = []
    offset = 0.0
    for i, chunk in enumerate(chunks):
        actual, part_words = generate_aligned_part(request, chunk, i, len(chunks), output, reference, progress)
        duration = len(actual) / _tts.sr
        words.extend({"text": w["text"], "start": round(w["start"] + offset, 4),
                      "end": round(w["end"] + offset, 4)} for w in part_words)
        audio_parts.append(actual)
        offset += duration
        if i + 1 < len(chunks):
            pause = np.zeros(round(0.14 * _tts.sr), dtype=np.float32)
            audio_parts.append(pause)
            offset += len(pause) / _tts.sr
    # Preserve every synthesized sample and a short safety tail. Composer adds the video end hold.
    audio_parts.append(np.zeros(round(0.25 * _tts.sr), dtype=np.float32))
    final = np.concatenate(audio_parts)
    with wave.open(str(output), "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(_tts.sr)
        stream.writeframes((np.clip(final, -1, 1) * 32767).astype("<i2").tobytes())
    if not audio_only:
        write_subtitles(words, request["srt"])
    return {"success": True, "audio": str(output), "srt": "" if audio_only else request["srt"],
            "duration": len(final) / _tts.sr, "word_count": len(words), "device": _device,
            "audio_only": audio_only, "alignment_loaded": _aligner is not None,
            "timing": {"load_seconds": round(loaded - started, 3),
                       "generate_seconds": round(time.perf_counter() - loaded, 3)}}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    args = parser.parse_args()
    root = Path(args.root).resolve()
    os.environ["HF_HOME"] = str(root / "cache" / "huggingface")
    os.environ["NUMBA_CACHE_DIR"] = str(root / "cache" / "numba")
    for line in sys.stdin:
        request = {}
        try:
            request = json.loads(line)
            if request.get("command") == "shutdown":
                return
            # Third-party libraries often print; reserve stdout for the private JSON protocol.
            with contextlib.redirect_stdout(sys.stderr):
                result = synthesize(request, root, lambda m: print(json.dumps({"id": request.get("id"), "progress": m}, ensure_ascii=False), file=protocol, flush=True))
            result["id"] = request.get("id")
            emit(result)
        except Exception as exc:
            traceback.print_exc(file=sys.stderr)
            emit({"id": request.get("id"), "success": False, "error": str(exc)})


if __name__ == "__main__":
    protocol = sys.stdout
    main()
