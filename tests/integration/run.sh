#!/usr/bin/env bash
# Usage: tests/integration/run.sh IMAGE PUBLISH_DIR PLUGIN_GUID (KEEP=1 keeps the server).
# Offline server, remote fetchers off: posters and media info come only from the fixtures.
set -euo pipefail

image=$1 publish=$2 guid=$3
root=$(cd "$(dirname "$0")/../.." && pwd)
work=$(mktemp -d)
container=jellytag-it-$$
failures=0
token=

cleanup() {
    if ((failures > 0)); then
        echo "--- JellyTag and error lines of the server log"
        podman logs "$container" 2>&1 | grep -E 'JellyTag|\[ERR\]' | tail -50 || true
    fi
    if [[ -n ${KEEP:-} ]]; then
        echo "kept: container $container, data $work"
    else
        podman rm -f "$container" >/dev/null 2>&1 || true
        # Files written by the container belong to the rootless user namespace.
        podman unshare rm -rf "$work"
    fi
}
trap cleanup EXIT

api() { # METHOD PATH [JSON BODY]
    local auth='MediaBrowser Client="integration", Device="integration", DeviceId="integration", Version="1"'
    [[ -n $token ]] && auth+=", Token=\"$token\""
    local args=(-sSf -X "$1" -H 'Content-Type: application/json' -H "Authorization: $auth")
    [[ $# -ge 3 ]] && args+=(-d "$3")
    podman exec "$container" curl "${args[@]}" "http://localhost:8096$2"
}

upload() { # BADGE_KEY, SVG on stdin
    local auth="MediaBrowser Client=\"integration\", Device=\"integration\", DeviceId=\"integration\", Version=\"1\", Token=\"$token\""
    podman exec -i "$container" curl -sSf -X POST -H "Authorization: $auth" \
        -F "file=@-;type=image/svg+xml;filename=custom.svg" "http://localhost:8096/JellyTag/CustomBadge/$1"
}

wait_for() { # SECONDS DESCRIPTION COMMAND...
    local deadline=$((SECONDS + $1)) what=$2
    shift 2
    until "$@" >/dev/null 2>&1; do
        ((SECONDS < deadline)) || {
            echo "timeout waiting for $what" >&2
            exit 1
        }
        sleep 1
    done
}

check() { # DESCRIPTION EXPECTED ACTUAL
    if [[ $2 == "$3" ]]; then
        echo "ok   $1"
    else
        echo "FAIL $1: expected $2, got $3"
        failures=$((failures + 1))
    fi
}

# --- Server with the plugin installed

mkdir -p "$work"/{config/plugins/JellyTag,cache}
"$root/tests/fixtures/media.sh" "$work/media"
cp "$publish"/*.dll "$work/config/plugins/JellyTag/"
# Without the real GUID, Jellyfin derives the plugin id from the folder name.
jq -n --arg guid "$guid" '{guid: $guid, name: "JellyTag", version: "0.0.0.0",
  targetAbi: "12.0.0.0", owner: "Atilili", category: "General"}' \
    >"$work/config/plugins/JellyTag/meta.json"

podman run -d --name "$container" --network none \
    -v "$work/config:/config:Z" -v "$work/cache:/cache:Z" -v "$work/media:/media:ro,Z" \
    "$image" >/dev/null

# /health answers while the startup page is still served: wait for the JSON API.
api_ready() { api GET /System/Info/Public | jq -e .Id; }
wait_for 180 "the Jellyfin API" api_ready

api POST /Startup/Configuration '{"UICulture":"en-US","MetadataCountryCode":"US","PreferredMetadataLanguage":"en"}'
api GET /Startup/User >/dev/null
api POST /Startup/User '{"Name":"admin","Password":"admin"}'
api POST /Startup/Complete
login=$(api POST /Users/AuthenticateByName '{"Username":"admin","Pw":"admin"}')
token=$(jq -r .AccessToken <<<"$login")

check "plugin loaded" Active "$(api GET /Plugins | jq -r '.[] | select(.Name == "JellyTag") | .Status')"

api POST "/Library/VirtualFolders?name=movies&collectionType=movies&paths=/media/movies&refreshLibrary=false" \
    '{"LibraryOptions":{"TypeOptions":[{"Type":"Movie","MetadataFetchers":[],"ImageFetchers":[]}]}}'
api POST /Library/Refresh

scan_done() {
    [[ $(api GET "/Items?Recursive=true&IncludeItemTypes=Movie" | jq .TotalRecordCount) == 12 ]] &&
        api GET /ScheduledTasks | jq -e '.[] | select(.Key == "RefreshLibrary") | .State == "Idle"'
}
wait_for 180 "the library scan" scan_done

item() { # TITLE
    api GET "/Items?Recursive=true&IncludeItemTypes=Movie&Fields=Path" |
        jq -er --arg path "/media/movies/$1 (2024)/" '[.Items[] | select(.Path | startswith($path)) | .Id]
      | if length == 1 then .[0] else error("\(length) items match \($path)") end'
}
served() { api GET "/Items/$1/Images/Primary" | md5sum | cut -d' ' -f1; }
original() { md5sum "$work/media/movies/$1 (2024)/poster.jpg" | cut -d' ' -f1; }
# No API exposes the detection: the middleware logs it once per rendered image.
# Sorted: the order of the keys is internal (cache key), each panel sorts its own badges.
badges() { podman logs "$container" 2>&1 | grep -F "badges to $1: " | tail -1 | sed 's/.*: //' | sorted; }
sorted() { tr _ '\n' | sort | paste -sd_; }

# --- Badges drawn from the media info Jellyfin read from the files

declare -A expected=(
    ["JT 4K"]=4k_h264_5.1_eng
    ["JT 1080p"]=1080p_h264_stereo_fra
    ["JT 1080p 4-3"]=1080p_h264_stereo_eng
    ["JT 720p"]=720p_h264_7.1_deu
    ["JT SD"]=sd_h264_jpn
    ["JT VFQ"]=1080p_h264_stereo_vq
    ["JT VFF"]=1080p_h264_stereo_vff
    ["JT Long"]=sd_h264_stereo_eng
    ["JT Multi"]=4k_1080p_h264_5.1_stereo_eng_fra
    ["JT Default"]=1080p_h264_stereo_deu_eng_jpn
    ["JT HDR 3D"]=1080p_hevc_hdr10_3d_stereo_eng
    # English subtitles hidden: the English audio is there.
    ["JT Subs"]=1080p_h264_stereo_eng_subfra
)
declare -A rendered
for title in "${!expected[@]}"; do
    id=$(item "$title")
    rendered[$title]=$(served "$id")
    check "$title: poster has badges" true "$([[ ${rendered[$title]} != "$(original "$title")" ]] && echo true || echo false)"
    check "$title: detected badges" "$(sorted <<<"${expected[$title]}")" "$(badges "$title")"
done

# The language panel keeps the order of detection: default track first, then alphabetical.
languages() { podman logs "$container" 2>&1 | grep -F "badges to $1: " | tail -1 | sed 's/.*: //' | tr _ '\n' | grep -xE 'deu|eng|jpn' | paste -sd_; }
check "JT Default: default audio language first" jpn_deu_eng "$(languages "JT Default")"

# --- Cache, then the plugin switched off

title="JT 4K"
id=$(item "$title")
check "cache: one rendered image per poster" 12 "$(api GET /JellyTag/CacheStats | jq .FileCount)"
check "cache: same image on the next request" "${rendered[$title]}" "$(served "$id")"

plugin=$(api GET /Plugins | jq -r '.[] | select(.Name == "JellyTag") | .Id')

# --- Optional video length badge, off by default

api POST "/Plugins/$plugin/Configuration" "$(api GET "/Plugins/$plugin/Configuration" | jq -c '.PosterConfig.RuntimePanel.Enabled = true')"
title="JT Long"
id=$(item "$title")
before=${rendered[$title]}
check "runtime: poster rendered again" true "$([[ $(served "$id") != "$before" ]] && echo true || echo false)"
check "runtime: badge added" "$(sorted <<<"${expected[$title]}_runtime")" "$(badges "$title")"

# --- HDR panel: "Highest only" (default) draws HDR10 alone, "All" adds the 3D badge

api POST "/Plugins/$plugin/Configuration" "$(api GET "/Plugins/$plugin/Configuration" | jq -c '.PosterConfig.HdrPanel.ShowMode = "All"')"
title="JT HDR 3D"
check "hdr panel all: second badge drawn" true "$([[ $(served "$(item "$title")") != "${rendered[$title]}" ]] && echo true || echo false)"
title="JT 1080p"
check "hdr panel all: poster without HDR unchanged" "${rendered[$title]}" "$(served "$(item "$title")")"

# --- Language filters (#6) and subtitle panel (#7)

config() { api POST "/Plugins/$plugin/Configuration" "$(api GET "/Plugins/$plugin/Configuration" | jq -c "$1")"; }
title="JT Subs"
id=$(item "$title")
config '.PosterConfig.LanguagePanel.LanguageFilter = "Except" | .PosterConfig.LanguagePanel.LanguageCodes = ["eng"]'
served "$id" >/dev/null
check "language filter: English audio hidden" "$(sorted <<<1080p_h264_stereo_subfra)" "$(badges "$title")"
config '.PosterConfig.SubtitlePanel.SubtitlesWithoutAudioOnly = false'
served "$id" >/dev/null
check "subtitle panel: all subtitle languages" "$(sorted <<<1080p_h264_stereo_subeng_subfra)" "$(badges "$title")"
config '.PosterConfig.LanguagePanel.LanguageFilter = "All" | .PosterConfig.SubtitlePanel.SubtitlesWithoutAudioOnly = true'

# --- Centered position (#22): the resolution badge moves, moving it back restores the poster

title="JT 4K"
id=$(item "$title")
position=$(api GET "/Plugins/$plugin/Configuration" | jq -r .PosterConfig.ResolutionPanel.Position)
config '.PosterConfig.ResolutionPanel.Position = "TopCenter"'
check "centered position: saved" TopCenter "$(api GET "/Plugins/$plugin/Configuration" | jq -r .PosterConfig.ResolutionPanel.Position)"
check "centered position: poster drawn again" true "$([[ $(served "$id") != "${rendered[$title]}" ]] && echo true || echo false)"
config ".PosterConfig.ResolutionPanel.Position = \"$position\""
check "centered position: previous poster back" "${rendered[$title]}" "$(served "$id")"

# --- Custom flag: an uploaded flag-eng replaces the English flag, deleting it restores the default

title="JT 4K"
id=$(item "$title")
custom='<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 90 60"><rect width="90" height="60" fill="#b22234"/></svg>'
upload flag-eng <<<"$custom"
check "custom flag: listed" true "$(api GET /JellyTag/CustomBadges | jq 'any(. == "flag-eng")')"
check "custom flag: preview serves it" "$(md5sum <<<"$custom" | cut -d' ' -f1)" "$(api GET /JellyTag/BadgePreview/flag-eng | md5sum | cut -d' ' -f1)"
check "custom flag: poster drawn with it" true "$([[ $(served "$id") != "${rendered[$title]}" ]] && echo true || echo false)"
api DELETE /JellyTag/CustomBadge/flag-eng
check "custom flag: default flag back" "${rendered[$title]}" "$(served "$id")"

api POST "/Plugins/$plugin/Configuration" "$(api GET "/Plugins/$plugin/Configuration" | jq -c '.Enabled = false')"
check "disabled: original poster served" "$(original "$title")" "$(served "$id")"

((failures == 0)) || exit 1
echo "all passed"
