"""Fixed PrusaSlicer 2.x INI adapter. No client scripts, profiles or executables."""
import hashlib
import json
import math
import re
import subprocess
import sys
from pathlib import Path


def parse_summary(text):
    def one(pattern):
        matches = re.findall(pattern, text, re.MULTILINE)
        if len(matches) != 1:
            raise ValueError("Missing or ambiguous slicing metadata")
        return matches[0].strip()

    # Total already includes individual extruders/supports/tower. Never add the
    # individual values or tower field again. Unreported startup purge is an
    # explicit audited profile correction applied once by the application.
    grams = float(one(r"^; total filament used \[g\] = ([0-9.]+)\r?$"))
    per_extruder = one(r"^; filament used \[g\] = ([0-9., ]+)\r?$")
    parts = [float(p.strip()) for p in per_extruder.split(",")]
    if len(parts) != 1 or abs(sum(parts) - grams) > 0.05:
        raise ValueError("Unsupported multiple extruders or inconsistent totals")
    time = one(r"^; estimated printing time \(normal mode\) = (.+)$")
    if not re.fullmatch(r"(?:\d+d\s*)?(?:\d+h\s*)?(?:\d+m\s*)?(?:\d+s\s*)?", time):
        raise ValueError("Unknown time format")
    seconds = sum(int(v) * {"d": 86400, "h": 3600, "m": 60, "s": 1}[u]
                  for v, u in re.findall(r"(\d+)([dhms])", time))
    if not math.isfinite(grams) or not 0 < grams <= 100000 or not 0 < seconds <= 10000000:
        raise ValueError("Out of bounds")
    blocks = re.findall(r"; prusaslicer_config = begin\r?\n(.*?); prusaslicer_config = end", text, re.S)
    if len(blocks) != 1:
        raise ValueError("Missing effective configuration")
    return {"grams": grams, "seconds": seconds,
            "effectiveConfigHash": hashlib.sha256(blocks[0].encode()).hexdigest()}


def main():
    if len(sys.argv) != 3:
        raise ValueError("Expected fixed bed center")
    center = [float(value) for value in sys.argv[1:]]
    if any(not math.isfinite(v) or not 0 < v <= 2500 for v in center):
        raise ValueError("Invalid bed center")
    ini = Path("/input/profile.ini").read_text(encoding="utf-8")
    if len(ini.encode()) > 1000000 or re.search(r"^[ \t]*(post_process|print_host|printhost_apikey)[ \t]*=[ \t]*[^\s\r\n]", ini.lstrip("\ufeff"), re.M | re.I):
        raise ValueError("Unsafe profile")
    version = subprocess.run(["prusa-slicer", "--version"], check=True,
                             capture_output=True, text=True, timeout=10).stdout.strip()
    if not version.startswith("PrusaSlicer-2."):
        raise ValueError("Only verified 2.x INI adapter supported")
    # stdout/stderr never enter application logs; tmpfs bounds all slicer output.
    subprocess.run(["prusa-slicer", "--load", "/input/profile.ini", "--export-gcode",
                    "--center", f"{center[0]},{center[1]}", "--output", "/tmp/model.gcode",
                    "/input/model.stl"], check=True, stdout=subprocess.DEVNULL,
                   stderr=subprocess.DEVNULL, timeout=240)
    output = Path("/tmp/model.gcode")
    if not 0 < output.stat().st_size <= 128000000:
        raise ValueError("Output limit")
    summary = parse_summary(output.read_text(encoding="utf-8"))
    summary["engineVersion"] = version
    print(json.dumps(summary, allow_nan=False))


if __name__ == "__main__":
    try:
        main()
    except Exception:
        # Do not echo profile contents, process arguments or private paths.
        print("Slicing requires technical review", file=sys.stderr)
        sys.exit(1)
