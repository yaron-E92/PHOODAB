#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: bash phoodab/apps/mobile/scripts/maui-android-ubuntu.sh <doctor|build|run> [dotnet args...]

Commands:
  doctor  Check Ubuntu prerequisites for the MAUI Android path.
  build   Build the MAUI app for net10.0-android.
  run     Build and deploy the MAUI app to an attached Android device or emulator.

Examples:
  bash phoodab/apps/mobile/scripts/maui-android-ubuntu.sh doctor
  bash phoodab/apps/mobile/scripts/maui-android-ubuntu.sh build -c Debug
  bash phoodab/apps/mobile/scripts/maui-android-ubuntu.sh run -c Debug
EOF
}

fail() {
  printf 'ERROR: %s\n\n' "$1" >&2
  shift
  printf '%s\n' "$@" >&2
  exit 1
}

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_path="$(cd -- "${script_dir}/.." && pwd)/Phoodab.Mobile.csproj"
command_name="${1:-doctor}"

case "${command_name}" in
  doctor|build|run)
    shift || true
    ;;
  -h|--help|help)
    usage
    exit 0
    ;;
  *)
    usage >&2
    exit 2
    ;;
esac

if ! command -v dotnet >/dev/null 2>&1; then
  fail "The dotnet CLI was not found." \
    "Install the .NET SDK required by this repository and make sure dotnet is on PATH."
fi

if ! dotnet workload list 2>/dev/null | awk '{ print $1 }' | grep -Eq '^(maui|maui-android)$'; then
  fail "The .NET MAUI Android workload is not installed." \
    "Install it with:" \
    "  dotnet workload install maui-android" \
    "Then retry this command."
fi

android_sdk_root="${ANDROID_SDK_ROOT:-${ANDROID_HOME:-}}"
if [[ -z "${android_sdk_root}" || ! -d "${android_sdk_root}" ]]; then
  fail "The Android SDK directory was not found." \
    "Install the Android SDK, then export one of:" \
    "  export ANDROID_SDK_ROOT=/path/to/android-sdk" \
    "  export ANDROID_HOME=/path/to/android-sdk"
fi

adb_path="${android_sdk_root}/platform-tools/adb"
if [[ "${command_name}" == "run" ]]; then
  if [[ ! -x "${adb_path}" ]]; then
    fail "Android adb was not found at ${adb_path}." \
      "Install Android SDK platform-tools, then retry with an emulator or device available."
  fi

  if ! "${adb_path}" devices | awk 'NR > 1 && $2 == "device" { found = 1 } END { exit found ? 0 : 1 }'; then
    fail "No ready Android emulator or attached device was found." \
      "Start an Android emulator or attach a USB device with debugging enabled, then retry run." \
      "Use '${adb_path} devices' to confirm that at least one entry is in the 'device' state."
  fi
fi

case "${command_name}" in
  doctor)
    printf 'Ubuntu MAUI Android prerequisites look available.\n'
    printf 'Project: %s\n' "${project_path}"
    printf 'Android SDK: %s\n' "${android_sdk_root}"
    ;;
  build)
    dotnet build "${project_path}" -f net10.0-android "$@"
    ;;
  run)
    dotnet build "${project_path}" -f net10.0-android -t:Run "$@"
    ;;
esac
