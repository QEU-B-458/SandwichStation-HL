#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TIMESTAMP="$(date -u +%Y%m%d_%H%M%S)"
LOG_ROOT="${ROOT_DIR}/logs/runtime"
LOG_DIR="${LOG_ROOT}/${TIMESTAMP}"

BUILD=1
CONFIGURATION="Debug"

usage() {
    cat <<EOF
Usage: $(basename "$0") [--no-build] [--release] [--log-dir PATH]

Builds Content.Server and Content.Client, launches both, and writes logs to a
timestamped folder. Press Ctrl+C to stop both processes.

Options:
  --no-build       Skip the dotnet build step.
  --release        Build using the Release configuration.
  --log-dir PATH   Override the output log directory.
  -h, --help       Show this help text.
EOF
}

print_log_summary() {
    local name="$1"
    local log_file="$2"

    echo
    echo "${name} log: ${log_file}"

    if [[ ! -s "${log_file}" ]]; then
        echo "No ${name,,} log output was captured."
        return
    fi

    local fatal_line
    fatal_line="$(grep -E '\[(FATL|ERR|ERROR)\]' "${log_file}" | tail -n 1 || true)"

    if [[ -n "${fatal_line}" ]]; then
        echo "Last ${name,,} error:"
        echo "  ${fatal_line}"
    else
        echo "Last ${name,,} log lines:"
        tail -n 10 "${log_file}" | sed 's/^/  /'
    fi

    if grep -q 'Address already in use' "${log_file}"; then
        echo "Hint: port 1212 is already in use by another server process."
    fi
}

while (($# > 0)); do
    case "$1" in
        --no-build)
            BUILD=0
            shift
            ;;
        --release)
            CONFIGURATION="Release"
            shift
            ;;
        --log-dir)
            if (($# < 2)); then
                echo "--log-dir requires a path" >&2
                exit 1
            fi
            LOG_DIR="$2"
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 1
            ;;
    esac
done

mkdir -p "$LOG_DIR"

SERVER_LOG="${LOG_DIR}/server.log"
CLIENT_LOG="${LOG_DIR}/client.log"
META_LOG="${LOG_DIR}/run_info.txt"
SHUTTING_DOWN=0

cleanup() {
    local exit_code=${1:-$?}

    if (( SHUTTING_DOWN )); then
        return
    fi

    SHUTTING_DOWN=1

    if [[ -n "${CLIENT_PID:-}" ]] && kill -0 "${CLIENT_PID}" 2>/dev/null; then
        kill -- "-${CLIENT_PID}" 2>/dev/null || kill "${CLIENT_PID}" 2>/dev/null || true
        wait "${CLIENT_PID}" 2>/dev/null || true
    fi

    if [[ -n "${SERVER_PID:-}" ]] && kill -0 "${SERVER_PID}" 2>/dev/null; then
        kill -- "-${SERVER_PID}" 2>/dev/null || kill "${SERVER_PID}" 2>/dev/null || true
        wait "${SERVER_PID}" 2>/dev/null || true
    fi

    return "$exit_code"
}

on_signal() {
    cleanup 130
    exit 130
}

trap on_signal INT TERM
trap 'cleanup $?' EXIT

{
    echo "timestamp_utc=${TIMESTAMP}"
    echo "root_dir=${ROOT_DIR}"
    echo "log_dir=${LOG_DIR}"
    echo "configuration=${CONFIGURATION}"
    echo "server_log=${SERVER_LOG}"
    echo "client_log=${CLIENT_LOG}"
} > "$META_LOG"

if (( BUILD )); then
    echo "Building server..."
    (cd "$ROOT_DIR" && dotnet build Content.Server/Content.Server.csproj -c "${CONFIGURATION}") | tee "${LOG_DIR}/build_server.log"
    echo "Building client..."
    (cd "$ROOT_DIR" && dotnet build Content.Client/Content.Client.csproj -c "${CONFIGURATION}") | tee "${LOG_DIR}/build_client.log"
fi

echo "Starting server..."
(
    cd "${ROOT_DIR}/bin/Content.Server"
    exec setsid ./Content.Server
) > "$SERVER_LOG" 2>&1 &
SERVER_PID=$!

sleep 2

echo "Starting client..."
(
    cd "${ROOT_DIR}/bin/Content.Client"
    exec setsid ./Content.Client
) > "$CLIENT_LOG" 2>&1 &
CLIENT_PID=$!

{
    echo "server_pid=${SERVER_PID}"
    echo "client_pid=${CLIENT_PID}"
} >> "$META_LOG"

echo "Logs are being written to:"
echo "  ${LOG_DIR}"
echo "Server log:"
echo "  ${SERVER_LOG}"
echo "Client log:"
echo "  ${CLIENT_LOG}"
echo "Press Ctrl+C to stop both processes."

while true; do
    server_alive=0
    client_alive=0

    if kill -0 "${SERVER_PID}" 2>/dev/null; then
        server_alive=1
    fi

    if kill -0 "${CLIENT_PID}" 2>/dev/null; then
        client_alive=1
    fi

    if (( server_alive == 0 && client_alive == 0 )); then
        break
    fi

    if (( server_alive == 0 && client_alive == 1 )); then
        echo "Server exited; stopping client..."
        echo "Log directory: ${LOG_DIR}"
        print_log_summary "Server" "${SERVER_LOG}"
        cleanup 1
        exit 1
    fi

    if (( client_alive == 0 && server_alive == 1 )); then
        echo "Client exited; stopping server..."
        echo "Log directory: ${LOG_DIR}"
        print_log_summary "Client" "${CLIENT_LOG}"
        cleanup 1
        exit 1
    fi

    sleep 1
done

echo "Both processes exited."
echo "Log directory: ${LOG_DIR}"
print_log_summary "Server" "${SERVER_LOG}"
print_log_summary "Client" "${CLIENT_LOG}"
