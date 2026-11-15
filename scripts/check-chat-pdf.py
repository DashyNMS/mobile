#!/usr/bin/env python3
"""CI-only: exercise the production iOS renderer and compare rendered facts with desktop."""
import base64
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import unicodedata

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "artifacts/chat-pdf"
DESKTOP_SHA = "88600f8c084953103cb1df59ccff9b0e800fbd2c"
SOURCES = {
    "src/DesktopNMS.Avalonia/Services/IntelligenceChatPdfWriter.cs": "e22e1a9ed0c2c4786f8bc2a327e3569f6b123395fa758399bf1159ea66677ed2",
    "src/DesktopNMS.Presentation/Services/Intelligence/ChatMarkdownBlocks.cs": "3b585f971addaab7af38e2583a8a69ca04ad68beda258e33c63702fd2f5657cf",
}


def run(*args, capture=False):
    return subprocess.check_output(args, cwd=ROOT, text=True).strip() if capture else subprocess.run(args, cwd=ROOT, check=True)


def desktop_reference():
    folder = ROOT / "tests/ChatPdf.Desktop/obj/desktop-reference"
    folder.mkdir(parents=True, exist_ok=True)
    for path, expected in SOURCES.items():
        encoded = run("gh", "api", f"repos/DashyNMS/desktop/contents/{path}?ref={DESKTOP_SHA}", "--jq", ".content", capture=True)
        source = base64.b64decode(encoded)
        assert hashlib.sha256(source).hexdigest() == expected, "Desktop source provenance mismatch"
        text = source.decode().replace("using Avalonia.Platform;", "using DashyNMS.Mobile.Intelligence;")
        text = text.replace('AssetLoader.Open(new Uri("avares://DashyNMS/Assets/Fonts/" + file))',
                            'File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fonts", file))')
        (folder / Path(path).name).write_text(text)
    run("dotnet", "run", "--project", "tests/ChatPdf.Desktop", "-c", "Release", "--",
        str(ROOT / "tests/ChatPdf/recorded-graph.png"), str(OUTPUT / "desktop.pdf"))


def native_pdf():
    run("dotnet", "build", "tests/ChatPdf.Native", "-c", "Debug")
    app = next((ROOT / "tests/ChatPdf.Native/bin/Debug/net10.0-ios/iossimulator-arm64").glob("*.app"))
    # Let the .NET SDK sign the executable and runtime dependencies in build order.
    # This is simulator-only ad-hoc signing, with no distribution credentials.
    run("codesign", "--verify", "--deep", "--strict", str(app))
    runtimes = json.loads(run("xcrun", "simctl", "list", "runtimes", "--json", capture=True))["runtimes"]
    available = {r["identifier"]: r for r in runtimes if r.get("isAvailable") and r["name"].startswith("iOS ")}
    devices = json.loads(run("xcrun", "simctl", "list", "devices", "available", "--json", capture=True))["devices"]
    # CoreSimulator's catalogue order is not newest-first, and device-specific runtimes
    # can reject even a recent phone. Reuse a pairing CoreSimulator already validated.
    candidates = [(runtime, phone) for runtime, phones in devices.items() if runtime in available
                  for phone in phones if phone.get("isAvailable") and phone["name"].startswith("iPhone")
                  and phone["state"] == "Shutdown"]
    assert candidates, "No available shutdown iPhone simulator with an installed iOS runtime"
    sdk_version = tuple(int(n) for n in run("xcrun", "--sdk", "iphonesimulator", "--show-sdk-version", capture=True).split(".")[:2])
    matching_sdk = [pair for pair in candidates if tuple(int(n) for n in available[pair[0]]["version"].split(".")[:2]) == sdk_version]
    assert matching_sdk, f"No available iPhone simulator matching the pinned Xcode SDK {sdk_version}"
    runtime, phone = matching_sdk[0]
    print(f"PDF simulator: {phone['name']} with {available[runtime]['name']}", flush=True)
    device = run("xcrun", "simctl", "clone", phone["udid"], "DashyNMS PDF regression", capture=True)
    try:
        run("xcrun", "simctl", "boot", device)
        run("xcrun", "simctl", "bootstatus", device, "-b")
        run("xcrun", "simctl", "install", device, str(app))
        installed = run("xcrun", "simctl", "get_app_container", device, "net.example.dashynms.pdfregression", "app", capture=True)
        run("codesign", "--verify", "--deep", "--strict", installed)
        with (OUTPUT / "native-signature.log").open("w") as log:
            subprocess.run(["codesign", "--display", "--verbose=4", "--entitlements", ":-", installed],
                           stdout=log, stderr=subprocess.STDOUT, check=True)
        run("xcrun", "simctl", "launch", device, "net.example.dashynms.pdfregression")
        container = Path(run("xcrun", "simctl", "get_app_container", device, "net.example.dashynms.pdfregression", "data", capture=True))
        result = container / "Documents/result.txt"
        deadline = time.monotonic() + 120
        while not result.exists() and time.monotonic() < deadline:
            time.sleep(1)
        assert result.exists(), "Native PDF harness timed out"
        shutil.copyfile(result, OUTPUT / "native-result.txt")
        assert result.read_text() == "OK", result.read_text()
        shutil.copyfile(container / "Documents/native.pdf", OUTPUT / "native.pdf")
    except Exception:
        # Keep only this synthetic harness's startup diagnostics, before deleting its simulator.
        predicate = 'process == "ChatPdf.Native" OR eventMessage CONTAINS "net.example.dashynms.pdfregression"'
        with (OUTPUT / "native-launch.log").open("w") as log:
            subprocess.run(["xcrun", "simctl", "spawn", device, "log", "show", "--last", "10m",
                            "--style", "compact", "--predicate", predicate], stdout=log, stderr=subprocess.STDOUT,
                           timeout=45, check=False)
        reports = Path.home() / "Library/Logs/DiagnosticReports"
        for report in reports.glob("ChatPdf.Native*.ips"):
            shutil.copyfile(report, OUTPUT / report.name)
        raise
    finally:
        subprocess.run(["xcrun", "simctl", "shutdown", device], check=False)
        subprocess.run(["xcrun", "simctl", "delete", device], check=False)


def verify_rendered_goldens():
    import pymupdf
    golden = json.loads((ROOT / "tests/ChatPdf/rendered.golden.json").read_text())
    facts = {}
    for name in ("desktop", "native"):
        doc = pymupdf.open(OUTPUT / f"{name}.pdf")
        pages = []
        for index, page in enumerate(doc):
            lines = page.get_text().splitlines()
            if name == "desktop":
                # Desktop's footer interrupts paragraphs continued on the next
                # page. Remove only this fixture's exact, numbered footer.
                footer = f"Recorded export · Page {index + 1}"
                assert lines and lines[-1].strip() == footer, f"{name}: missing page footer {index + 1}"
                lines.pop()
            pages.append(" ".join(lines))
        text = " ".join(pages)
        # Native shaping may extract typographic ligatures (for example ﬁ) as one
        # character. Compare their equivalent Unicode text, not glyph encoding.
        normalized = "".join(unicodedata.normalize("NFKC", text).split())
        assert len(doc) >= golden["minimum_pages"], f"{name}: pagination missing"
        for expected in golden["required_text"]:
            assert "".join(unicodedata.normalize("NFKC", expected).split()) in normalized, f"{name}: missing visible content: {expected}"
        for i in range(golden["records"]):
            body = f"Record{i:03}: Synthetic network evidence retained in the exported conversation. Every paragraph must survive pagination and remain selectable."
            assert "".join(unicodedata.normalize("NFKC", body).split()) in normalized, f"{name}: paragraph {i} lost across pages"
        images = sum(len(page.get_images()) for page in doc)
        assert images >= golden["minimum_images"], f"{name}: graph missing from PDF"
        links = [link.get("uri", "") for page in doc for link in page.get_links()]
        assert golden["link"] in links, f"{name}: source link is not clickable"
        red_pixels = 0
        for index, page in enumerate(doc):
            pixmap = page.get_pixmap(colorspace=pymupdf.csRGB, alpha=False)
            pixmap.save(OUTPUT / f"{name}-page-{index+1}.png")
            samples = pixmap.samples
            red_pixels += sum(r > 130 and r > g * 1.5 and r > b * 1.5 for r, g, b in zip(samples[0::3], samples[1::3], samples[2::3]))
        assert red_pixels >= golden["minimum_red_graph_pixels"], f"{name}: graph pixels are absent/blank"
        facts[name] = {"pages": len(doc), "images": images, "links": links, "red_graph_pixels": red_pixels,
                       "recorded_paragraphs": golden["records"]}
    (OUTPUT / "comparison.json").write_text(json.dumps(facts, indent=2))
    print("Native and pinned desktop PDFs satisfy the recorded rendered-content golden.")


if __name__ == "__main__":
    if sys.platform != "darwin" or not os.environ.get("CI"):
        raise SystemExit("This native PDF check runs only on the macOS GitHub Actions runner.")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    desktop_reference()
    native_pdf()
    verify_rendered_goldens()
