#!/usr/bin/env bash
# Usage: tests/fixtures/media.sh DIR. Test movies covering resolutions, audio
# layouts and languages, each with a poster.
set -euo pipefail

media=$1/movies

# A global LD_LIBRARY_PATH (alsa-lib from the host profile) breaks the flake ffmpeg.
ff() { env -u LD_LIBRARY_PATH ffmpeg -loglevel error -y "$@"; }

movie() { # $1 title, $2 resolution, $3 audio layout, $4 audio language, [$5 version suffix], [$6 audio title]
    local dir="$media/$1 (2024)"
    mkdir -p "$dir"
    [[ -f "$dir/$1 (2024)${5:-}.mkv" ]] || ff \
        -f lavfi -i "testsrc=duration=2:size=$2:rate=24" \
        -f lavfi -i "anullsrc=channel_layout=$3:sample_rate=48000" -t 2 \
        -c:v libx264 -pix_fmt yuv420p -c:a aac -metadata:s:a:0 "language=$4" -metadata:s:a:0 "title=${6:-}" \
        "$dir/$1 (2024)${5:-}.mkv"
    [[ -f "$dir/poster.jpg" ]] || ff -f lavfi -i "testsrc2=size=1000x1500" -frames:v 1 "$dir/poster.jpg"
    printf '<?xml version="1.0" encoding="utf-8" standalone="yes"?>\n<movie>\n  <title>%s</title>\n  <year>2024</year>\n</movie>\n' \
        "$1" >"$dir/$([[ -n ${5:-} ]] && echo movie || echo "$1 (2024)").nfo"
}

movie "JT 4K" 3840x2160 5.1 eng
movie "JT 1080p" 1920x1080 stereo fra
movie "JT 1080p 4-3" 1440x1080 stereo eng
movie "JT 720p" 1280x720 7.1 deu
movie "JT SD" 720x576 mono jpn
# Two versions of one movie: Jellyfin groups "<folder> - <suffix>" files in the same folder.
movie "JT Multi" 1920x1080 stereo fra " - 1080p"
movie "JT Multi" 3840x2160 5.1 eng " - 4K"
# French variants are told apart by the audio track title, the language is "fre" for both.
movie "JT VFQ" 1920x1080 stereo fre "" VFQ
movie "JT VFF" 1920x1080 stereo fre "" VFF
