#!/usr/bin/env bash
# Cross-compiles soloud.a for WebAssembly using Emscripten + the miniaudio
# backend, which drives WebAudio in the browser.
#
# Usage: build-native-wasm.sh
#
# Stages soloud.a into artifacts/native/browser-wasm/<emscripten-version>/.
#
# Static, not shared: a browser-wasm consumer links the archive into
# dotnet.native.wasm at publish time, so there is nothing to load at runtime.
# Keyed by Emscripten version because wasm objects only link against a runtime
# pack built with the same emsdk.
#
# By default the toolchain is taken from the .NET wasm workload, which is the
# emsdk that must be matched. Override with DOTNET_ROOT, or set EMSDK_PATH and
# the DOTNET_EMSCRIPTEN_* vars to use an emsdk installed elsewhere.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
SOLOUD="$REPO/external/soloud"
DOTNET_ROOT="${DOTNET_ROOT:-/usr/share/dotnet}"

if [ ! -f "$SOLOUD/include/soloud.h" ]; then
    echo "SoLoud submodule missing at $SOLOUD. Run bootstrap.ps1 first." >&2
    exit 1
fi

if [ -z "${EMSDK_PATH:-}" ]; then
    SDK_PACK="$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NET.Runtime.Emscripten.*.Sdk.*/*/tools 2>/dev/null | sort -V | tail -1)"
    if [ -z "$SDK_PACK" ]; then
        echo "No Emscripten SDK pack under $DOTNET_ROOT/packs." >&2
        echo "  Install it with: dotnet workload install wasm-tools" >&2
        exit 1
    fi
    export EMSDK_PATH="$SDK_PACK"
    export DOTNET_EMSCRIPTEN_LLVM_ROOT="$SDK_PACK/bin"
    export DOTNET_EMSCRIPTEN_BINARYEN_ROOT="$SDK_PACK"
    export DOTNET_EMSCRIPTEN_NODE_JS="$(find "$DOTNET_ROOT"/packs/Microsoft.NET.Runtime.Emscripten.*.Node.*/ -name node -type f 2>/dev/null | sort -V | tail -1)"
    # FROZEN_CACHE is on in the pack's .emscripten, so the prebuilt sysroot has
    # to be found or every compile fails trying to rebuild it.
    export EM_CACHE="$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NET.Runtime.Emscripten.*.Cache.*/*/tools/emscripten/cache 2>/dev/null | sort -V | tail -1)"
fi

if [ ! -x "${DOTNET_EMSCRIPTEN_NODE_JS:-}" ]; then
    echo "node not found — DOTNET_EMSCRIPTEN_NODE_JS=${DOTNET_EMSCRIPTEN_NODE_JS:-<unset>}" >&2
    exit 1
fi

# shellcheck source=/dev/null
source "$EMSDK_PATH/emsdk_env.sh" >/dev/null
export EMSCRIPTEN="$EMSDK_PATH/emscripten"

for tool in emcc em++ emar emcmake; do
    if [ ! -x "$EMSCRIPTEN/$tool" ]; then
        echo "$tool not found under $EMSCRIPTEN" >&2
        exit 1
    fi
done

EMVER="$(echo "$EMSDK_PATH" | grep -oE 'Emscripten\.[0-9]+\.[0-9]+\.[0-9]+' | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1)"
if [ -z "$EMVER" ]; then
    echo "Could not determine the Emscripten version from $EMSDK_PATH" >&2
    exit 1
fi

NPROC="$(getconf _NPROCESSORS_ONLN 2>/dev/null || echo 4)"
BUILD_DIR="$REPO/build/build-browser-wasm"
rm -rf "$BUILD_DIR"

echo "[build-native-wasm] emscripten $EMVER at $EMSCRIPTEN"
"$EMSCRIPTEN/emcmake" cmake -S "$REPO/build" -B "$BUILD_DIR" \
    -DCMAKE_BUILD_TYPE=Release \
    -DSOLOUD_LIBRARY_TYPE=STATIC

cmake --build "$BUILD_DIR" --parallel "$NPROC"

LIB="$(find "$BUILD_DIR" -maxdepth 3 -name 'libsoloud.a' -o -maxdepth 3 -name 'soloud.a' | head -1)"
if [ -z "$LIB" ]; then
    echo "Expected libsoloud.a under $BUILD_DIR" >&2
    find "$BUILD_DIR" -name '*.a' >&2 || true
    exit 1
fi

NATIVE_OUT="$REPO/artifacts/native/browser-wasm/$EMVER"
mkdir -p "$NATIVE_OUT"
# No lib prefix: the wasm pinvoke table matches [DllImport("soloud")] against
# the archive's file stem.
OUT="$NATIVE_OUT/soloud.a"
cp -f "$LIB" "$OUT"

# Catches a host toolchain silently building a native archive instead. od, not
# grep: the wasm magic leads with a NUL byte.
if [ "$(ar p "$OUT" "$(ar t "$OUT" | head -1)" | od -An -tx1 -N4 | tr -d ' ')" != "0061736d" ]; then
    echo "$OUT does not contain wasm objects" >&2
    exit 1
fi

# Capture nm output first; piping into `grep -q` would trip `set -o pipefail`
# (grep exits on first match, nm gets SIGPIPE).
syms="$("$DOTNET_EMSCRIPTEN_LLVM_ROOT/llvm-nm" --defined-only "$OUT" 2>/dev/null)"
if ! grep -qE '\bSoloud_create$' <<<"$syms"; then
    echo "soloud.a does not define Soloud_create" >&2
    exit 1
fi

echo "[build-native-wasm] symbol check OK — browser-wasm/$EMVER staged."
ls -la "$NATIVE_OUT"
