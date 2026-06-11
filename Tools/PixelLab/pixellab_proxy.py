"""
Local auth-injecting proxy for the PixelLab MCP server.

Why: Claude desktop custom connectors only support OAuth credentials, but
PixelLab's MCP wants a static `Authorization: Bearer <token>` header. This
proxy listens on localhost, forwards everything to https://api.pixellab.ai,
and injects the header. Point the Claude connector at:

    http://localhost:8787/mcp

Token is read from `pixellab_token.local.txt` next to this script (gitignored)
so the secret never lands in the repo.

Run:  py -3 pixellab_proxy.py   (or double-click run_pixellab_proxy.bat)
Stop: Ctrl+C in the console window.
"""

import http.client
import http.server
import os
import sys

UPSTREAM_HOST = "api.pixellab.ai"
PORT = 8787

TOKEN_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                          "pixellab_token.local.txt")

def load_token():
    try:
        with open(TOKEN_FILE, "r", encoding="utf-8") as f:
            tok = f.read().strip()
    except OSError:
        print(f"ERROR: token file not found: {TOKEN_FILE}")
        print("Create it containing exactly one line:  Bearer YOUR_API_TOKEN")
        sys.exit(1)
    if not tok.lower().startswith("bearer "):
        tok = "Bearer " + tok
    return tok

TOKEN = load_token()

HOP_HEADERS = {"connection", "keep-alive", "transfer-encoding",
               "content-encoding", "proxy-authenticate", "te", "trailers",
               "upgrade", "host", "content-length", "accept-encoding",
               "authorization"}


class Proxy(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, fmt, *args):
        print("[proxy]", fmt % args)

    def _forward(self):
        try:
            length = int(self.headers.get("Content-Length") or 0)
            body = self.rfile.read(length) if length > 0 else None

            headers = {k: v for k, v in self.headers.items()
                       if k.lower() not in HOP_HEADERS}
            headers["Host"] = UPSTREAM_HOST
            headers["Authorization"] = TOKEN
            headers["Accept-Encoding"] = "identity"
            if body is not None:
                headers["Content-Length"] = str(len(body))

            conn = http.client.HTTPSConnection(UPSTREAM_HOST, timeout=600)
            conn.request(self.command, self.path, body=body, headers=headers)
            resp = conn.getresponse()

            self.send_response(resp.status)
            content_length = resp.getheader("Content-Length")
            for k, v in resp.getheaders():
                if k.lower() in HOP_HEADERS or k.lower() == "content-length":
                    continue
                self.send_header(k, v)

            if content_length is not None:
                data = resp.read()
                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                if data:
                    self.wfile.write(data)
            else:
                # streamed (SSE) response: re-chunk to the client as it arrives
                self.send_header("Transfer-Encoding", "chunked")
                self.end_headers()
                while True:
                    try:
                        chunk = resp.read1(8192)
                    except AttributeError:
                        chunk = resp.read(8192)
                    if not chunk:
                        break
                    self.wfile.write(("%X\r\n" % len(chunk)).encode())
                    self.wfile.write(chunk)
                    self.wfile.write(b"\r\n")
                    self.wfile.flush()
                self.wfile.write(b"0\r\n\r\n")
            conn.close()
        except Exception as e:  # noqa: BLE001 - report anything to the client
            try:
                self.send_error(502, f"proxy error: {e}")
            except Exception:
                pass

    do_GET = _forward
    do_POST = _forward
    do_DELETE = _forward
    do_PUT = _forward
    do_PATCH = _forward

    def do_OPTIONS(self):
        self._forward()


def main():
    server = http.server.ThreadingHTTPServer(("127.0.0.1", PORT), Proxy)
    print(f"PixelLab MCP proxy running: http://localhost:{PORT}/mcp")
    print(f"Forwarding to https://{UPSTREAM_HOST} with auth header injected.")
    print("Leave this window open while using PixelLab in Claude. Ctrl+C to stop.")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopped.")


if __name__ == "__main__":
    main()
