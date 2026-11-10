"""Lay the showcase music under the recorded arrival and ending reels.

`Cutscenes > Record Showcase Reels` writes silent MP4s to Logs/showcase-reel/ (capture runs
on a locked frame clock, so live audio would drift). This adds the reels' music track,
faded in and out to each clip's length, and writes PharmaSynth-arrival.mp4 and
PharmaSynth-ending.mp4 beside them.

    python Tools/showcase-reel.py [path\\to\\ffmpeg.exe]
"""
import glob
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Logs", "showcase-reel")
MUSIC = os.path.join(ROOT, "Assets", "PharmaSynth", "Audio", "Background_Music", "MenuRoom", "music_1.mp3")


def find_ffmpeg():
    if len(sys.argv) > 1:
        return sys.argv[1]
    hits = glob.glob(os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "WinGet", "Packages", "**", "ffmpeg.exe"), recursive=True)
    return hits[0] if hits else "ffmpeg"


def duration(ffprobe, path):
    out = subprocess.run([ffprobe, "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
                         capture_output=True, text=True, check=True).stdout
    return float(out.strip())


def main():
    ffmpeg = find_ffmpeg()
    ffprobe = os.path.join(os.path.dirname(ffmpeg), "ffprobe.exe") if os.path.isabs(ffmpeg) else "ffprobe"
    for name in ("arrival", "ending"):
        silent = os.path.join(OUT, "PharmaSynth-%s-silent.mp4" % name)
        if not os.path.exists(silent):
            print("missing", silent)
            continue
        d = duration(ffprobe, silent)
        fade = "afade=t=in:d=1.5,afade=t=out:st=%.2f:d=2.5" % max(0.0, d - 2.5)
        final = os.path.join(OUT, "PharmaSynth-%s.mp4" % name)
        subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-i", silent, "-stream_loop", "-1", "-i", MUSIC,
                        "-filter_complex", "[1:a]volume=0.8,%s[a]" % fade, "-map", "0:v", "-map", "[a]",
                        "-t", "%.2f" % d, "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", final], check=True)
        print("%s  %.1f s" % (final, d))


if __name__ == "__main__":
    main()
