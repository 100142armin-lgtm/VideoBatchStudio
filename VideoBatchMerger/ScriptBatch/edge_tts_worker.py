# -*- coding: utf-8 -*-
"""
Edge-TTS Worker for VideoBatchStudio
Synthesizes speech using Microsoft Edge Neural Voices and generates accurate, short-rhythm SRT subtitles.
"""

import sys
import os
import argparse
import asyncio
import json
import traceback
import re
from datetime import timedelta

try:
    import edge_tts
except ImportError:
    print(json.dumps({"success": False, "error": "edge-tts is not installed in current Python environment."}))
    sys.exit(1)


def format_srt_time(td):
    total_seconds = int(td.total_seconds())
    hours = total_seconds // 3600
    minutes = (total_seconds % 3600) // 60
    seconds = total_seconds % 60
    millis = int(td.microseconds / 1000)
    return f"{hours:02d}:{minutes:02d}:{seconds:02d},{millis:03d}"


def is_cjk(ch):
    code = ord(ch)
    return (0x4E00 <= code <= 0x9FFF or
            0x3400 <= code <= 0x4DBF or
            0x20000 <= code <= 0x2A6DF or
            0xF900 <= code <= 0xFAFF or
            0x3040 <= code <= 0x309F or
            0x30A0 <= code <= 0x30FF or
            0xAC00 <= code <= 0xD7AF)


def build_smart_chunked_srt(words, original_text):
    """
    Groups word boundary events into dynamic short video subtitles.
    Rules:
    - Max 2 to 4 words per cue (or 4 to 8 CJK chars).
    - Natural clause break on punctuation (, . ! ? ; : — …).
    - Breaks on spoken pauses (silence gap >= 0.38s).
    - Smooth end time to eliminate subtitle flickering.
    """
    if not words:
        return ""

    has_cjk = any(is_cjk(c) for c in original_text)

    # Tokenize original text to extract punctuation
    raw_tokens = original_text.split() if (" " in original_text and not has_cjk) else list(original_text)

    # Map trailing punctuation to word indices
    punc_map = {}
    ti = 0
    clean_re = re.compile(r'[^\w\u4e00-\u9fff]', re.UNICODE)

    for wi, w in enumerate(words):
        w_clean = clean_re.sub('', w['text'].lower())
        while ti < len(raw_tokens):
            raw_t = raw_tokens[ti]
            raw_clean = clean_re.sub('', raw_t.lower())
            if w_clean in raw_clean or raw_clean in w_clean or not w_clean:
                m = re.search(r'([,.;:!?—…，。！？；：、~]+)$', raw_t)
                if m:
                    punc_map[wi] = m.group(1)
                ti += 1
                break
            ti += 1

    chunks = []
    current_chunk = []

    max_words_per_cue = 4 if not has_cjk else 5
    max_chars_per_cue = 18 if not has_cjk else 9

    for i, w in enumerate(words):
        start_sec = w['offset'] / 10000000.0
        end_sec = (w['offset'] + w['duration']) / 10000000.0
        text = w['text']
        punc = punc_map.get(i, "")

        current_chunk.append({
            'index': i,
            'text': text + punc,
            'clean': text,
            'start': start_sec,
            'end': end_sec,
            'has_punc': bool(punc)
        })

        should_close = False

        # 1. Punctuation closes chunk immediately
        if punc:
            should_close = True
        # 2. Spoken pause (gap >= 0.38s)
        elif i + 1 < len(words):
            next_start = words[i+1]['offset'] / 10000000.0
            if (next_start - end_sec) >= 0.38:
                should_close = True
        # 3. Target word or character count reached
        cur_chars = sum(len(x['text']) for x in current_chunk)
        if len(current_chunk) >= max_words_per_cue or cur_chars >= max_chars_per_cue:
            should_close = True

        if should_close or i == len(words) - 1:
            chunks.append(current_chunk)
            current_chunk = []

    srt_lines = []
    sep = "" if has_cjk else " "

    for ci, chunk in enumerate(chunks):
        c_start = chunk[0]['start']
        c_end = chunk[-1]['end']

        # Smooth end time: if gap to next chunk is small (<0.35s), extend end time to avoid flicker
        if ci + 1 < len(chunks):
            next_c_start = chunks[ci+1][0]['start']
            if 0 < (next_c_start - c_end) <= 0.35:
                c_end = next_c_start - 0.04

        chunk_text = sep.join(x['text'] for x in chunk).strip()
        if not chunk_text:
            continue

        start_td = timedelta(seconds=c_start)
        end_td = timedelta(seconds=c_end)

        srt_lines.append(f"{len(srt_lines) // 4 + 1}")
        srt_lines.append(f"{format_srt_time(start_td)} --> {format_srt_time(end_td)}")
        srt_lines.append(chunk_text)
        srt_lines.append("")

    return "\n".join(srt_lines)


async def synthesize(args):
    text = ""
    if args.text_file and os.path.exists(args.text_file):
        with open(args.text_file, "r", encoding="utf-8") as f:
            text = f.read().strip()
    elif args.text:
        text = args.text.strip()

    if not text:
        return {"success": False, "error": "Empty text provided."}

    voice = args.voice or "pt-BR-AntonioNeural"
    rate = args.rate or "+0%"
    pitch = args.pitch or "+0Hz"
    volume = args.volume or "+0%"
    # Force WordBoundary so Edge-TTS delivers fine-grained word timestamps
    boundary = "WordBoundary"

    output_audio = os.path.abspath(args.output_audio)
    output_srt = os.path.abspath(args.output_srt) if args.output_srt else ""

    os.makedirs(os.path.dirname(output_audio), exist_ok=True)
    if output_srt:
        os.makedirs(os.path.dirname(output_srt), exist_ok=True)

    communicate = edge_tts.Communicate(
        text=text,
        voice=voice,
        rate=rate,
        volume=volume,
        pitch=pitch,
        boundary=boundary
    )

    words = []
    submaker = edge_tts.SubMaker()
    total_audio_bytes = 0

    with open(output_audio, "wb") as af:
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                af.write(chunk["data"])
                total_audio_bytes += len(chunk["data"])
            elif chunk["type"] == "WordBoundary":
                words.append({
                    "offset": chunk["offset"],
                    "duration": chunk["duration"],
                    "text": chunk["text"]
                })
                try:
                    submaker.feed(chunk)
                except Exception:
                    pass
            elif chunk["type"] == "SentenceBoundary":
                try:
                    submaker.feed(chunk)
                except Exception:
                    pass

    if total_audio_bytes == 0:
        return {"success": False, "error": "Zero audio bytes synthesized. Check network connection."}

    # Generate SRT if requested
    if output_srt:
        srt_content = ""
        if words:
            try:
                srt_content = build_smart_chunked_srt(words, text)
            except Exception:
                srt_content = ""

        if not srt_content.strip():
            try:
                srt_content = submaker.get_srt()
            except Exception:
                srt_content = ""

        # Final fallback
        if not srt_content.strip():
            srt_content = f"1\n00:00:00,100 --> 00:00:05,000\n{text[:60]}\n"

        with open(output_srt, "w", encoding="utf-8") as sf:
            sf.write(srt_content)

    return {
        "success": True,
        "audio": output_audio,
        "srt": output_srt,
        "voice": voice,
        "bytes": total_audio_bytes,
        "word_count": len(words)
    }


def main():
    parser = argparse.ArgumentParser(description="Edge-TTS Worker")
    parser.add_argument("--text", type=str, default="")
    parser.add_argument("--text-file", type=str, default="")
    parser.add_argument("--voice", type=str, default="pt-BR-AntonioNeural")
    parser.add_argument("--rate", type=str, default="+0%")
    parser.add_argument("--pitch", type=str, default="+0Hz")
    parser.add_argument("--volume", type=str, default="+0%")
    parser.add_argument("--boundary", type=str, default="WordBoundary")
    parser.add_argument("--output-audio", type=str, required=True)
    parser.add_argument("--output-srt", type=str, default="")

    args = parser.parse_args()

    try:
        result = asyncio.run(synthesize(args))
        print(json.dumps(result, ensure_ascii=False))
    except Exception as e:
        print(json.dumps({"success": False, "error": str(e), "trace": traceback.format_exc()}, ensure_ascii=False))
        sys.exit(1)


if __name__ == "__main__":
    main()
