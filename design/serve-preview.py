"""Serve the saved overlay prototype locally using only Python's standard library."""

import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import sys


class PreviewHandler(SimpleHTTPRequestHandler):
    extensions_map = {
        **SimpleHTTPRequestHandler.extensions_map,
        ".html": "text/html; charset=utf-8",
        ".md": "text/plain; charset=utf-8",
    }


class PreviewServer(ThreadingHTTPServer):
    # On Windows SO_REUSEADDR can let a second server take an occupied port.
    allow_reuse_address = sys.platform != "win32"


def port_number(value):
    try:
        port = int(value)
    except ValueError:
        raise argparse.ArgumentTypeError("port must be a number between 1 and 65535")
    if not 1 <= port <= 65535:
        raise argparse.ArgumentTypeError("port must be between 1 and 65535")
    return port


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=port_number, default=8765, help="local port (default: 8765)")
    args = parser.parse_args()
    preview_dir = Path(__file__).resolve().parent / "overlay-review"
    if not (preview_dir / "index.html").is_file():
        print("Preview index.html was not found beside this repository's design files.", file=sys.stderr)
        return 1

    handler = partial(PreviewHandler, directory=str(preview_dir))
    try:
        server = PreviewServer(("127.0.0.1", args.port), handler)
    except OSError as error:
        print(f"Could not start preview on port {args.port}: {error}", file=sys.stderr)
        print("Try a different port with --port.", file=sys.stderr)
        return 1

    with server:
        print(f"Overlay preview: http://127.0.0.1:{args.port}/", flush=True)
        print("Press Ctrl+C to stop.", flush=True)
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            print("\nPreview stopped.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
