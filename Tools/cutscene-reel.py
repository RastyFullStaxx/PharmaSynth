"""Assemble the cutscene reel recorded by `Cutscenes > Record Cutscene Reel`.

Reads Logs/cutscene-reel/manifest.txt (clips, frame counts, voice lines with their
frame offsets), puts a title slate before every clip, lays each Pharmee voice clip
back at the frame it started on (the recorder runs at a locked 30 fps, so game time
and the audio clock are decoupled and the audio has to be rebuilt), and joins it all
in campaign order into Logs/cutscene-reel/PharmaSynth-cutscenes.mp4.

Usage: python Tools/cutscene-reel.py [path-to-ffmpeg]
"""
import os, re, shutil, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Logs", "cutscene-reel")
FPS, SLATE = 30, 2.5
FONT = "C\\:/Windows/Fonts/arial.ttf"

ORDER = ["tutorial-methane", "prelim-chemical-compounding", "prelim-ethyl-alcohol", "midterm-benzoic-acid",
         "midterm-acetanilide", "midterm-acetone", "midterm-chloroform", "final-benzamide", "final-winemaking"]
NAMES = {"tutorial-methane": "Tutorial - Methane", "prelim-chemical-compounding": "Exp 2 - Chemical Compounding",
         "prelim-ethyl-alcohol": "Exp 3 - Ethyl Alcohol", "midterm-benzoic-acid": "Exp 4 - Benzoic Acid",
         "midterm-acetanilide": "Exp 5 - Acetanilide", "midterm-acetone": "Exp 6 - Acetone",
         "midterm-chloroform": "Exp 7 - Chloroform", "final-benzamide": "Exp 8 - Benzamide",
         "final-winemaking": "Exp 9 - Wine Making"}
KIND_RANK = {"Intro": -1, "Preparations complete": 0, "timelapse": 1, "Experiment complete (pass)": 2, "Experiment over (fail)": 3}


def ffmpeg_path():
    if len(sys.argv) > 1:
        return sys.argv[1]
    return shutil.which("ffmpeg") or "ffmpeg"


def parse():
    clips, cur = [], None
    for line in open(os.path.join(OUT, "manifest.txt"), encoding="utf-8"):
        f = line.rstrip("\n").split("\t")
        if f[0] == "clip":
            cur = {"index": int(f[1]), "file": os.path.join(ROOT, f[2].replace("\\", "/")), "label": f[3], "voices": [], "frames": 0}
            clips.append(cur)
        elif f[0] == "voice" and cur is not None and f[2]:
            cur["voices"].append((int(f[1]), os.path.join(ROOT, f[2])))
        elif f[0] == "frames" and cur is not None:
            cur["frames"] = int(f[2])
    for c in clips:
        c["voices"] = [v for v in c["voices"] if v[0] < c["frames"]]   # lines spoken between clips
        module, _, kind = c["label"].partition("  -  ")
        c["module"], c["kind"] = module, kind
        rank = next((r for k, r in KIND_RANK.items() if kind.startswith(k)), 9)
        c["sort"] = (ORDER.index(module) if module in ORDER else 99, rank, c["index"])
    return sorted(clips, key=lambda c: c["sort"])


def segment(ff, c, n, total):
    title = os.path.join(OUT, f"slate{n:02d}.txt")
    kind = c["kind"].replace("timelapse: ", "Timelapse - ")
    with open(title, "w", encoding="utf-8") as fh:
        fh.write(f"{NAMES.get(c['module'], c['module'])}\n{kind[0].upper() + kind[1:]}")
    tf = title.replace("\\", "/").replace(":", "\\:")
    dur = SLATE + c["frames"] / FPS
    args = [ff, "-y", "-loglevel", "error",
            "-f", "lavfi", "-t", str(SLATE), "-i", f"color=c=black:s=1920x1080:r={FPS}",
            "-i", c["file"]]
    for _, path in c["voices"]:
        args += ["-i", path]
    fc = (f"[0:v]drawtext=fontfile='{FONT}':textfile='{tf}':fontcolor=white:fontsize=60:line_spacing=24:"
          f"x=(w-text_w)/2:y=(h-text_h)/2,"
          f"drawtext=fontfile='{FONT}':text='{n} / {total}':fontcolor=gray:fontsize=28:x=(w-text_w)/2:y=h-90[s];"
          f"[s][1:v]concat=n=2:v=1:a=0[v];")
    if c["voices"]:
        mix = []
        for i, (frame, _) in enumerate(c["voices"]):
            ms = int((SLATE + frame / FPS) * 1000)
            fc += f"[{i + 2}:a]aresample=48000,aformat=channel_layouts=stereo,adelay={ms}|{ms}[a{i}];"
            mix.append(f"[a{i}]")
        fc += f"{''.join(mix)}amix=inputs={len(mix)}:normalize=0,apad,atrim=0:{dur:.3f}[a]"
    else:
        fc += f"anullsrc=r=48000:cl=stereo,atrim=0:{dur:.3f}[a]"
    out = os.path.join(OUT, f"seg{n:02d}.mp4")
    args += ["-filter_complex", fc, "-map", "[v]", "-map", "[a]",
             "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-r", str(FPS),
             "-c:a", "aac", "-b:a", "160k", "-ar", "48000", out]
    subprocess.run(args, check=True)
    return out


def main():
    ff = ffmpeg_path()
    clips = parse()
    segs = [segment(ff, c, n + 1, len(clips)) for n, c in enumerate(clips)]
    lst = os.path.join(OUT, "segments.txt")
    with open(lst, "w", encoding="utf-8") as fh:
        for s in segs:
            fh.write(f"file '{s.replace(os.sep, '/')}'\n")
    reel = os.path.join(OUT, "PharmaSynth-cutscenes.mp4")
    subprocess.run([ff, "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", lst, "-c", "copy",
                    "-movflags", "+faststart", reel], check=True)
    secs = sum(SLATE + c["frames"] / FPS for c in clips)
    print(f"{len(clips)} clips, {secs / 60:.1f} min -> {reel}")


if __name__ == "__main__":
    main()
