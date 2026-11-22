#!/usr/bin/env bash
# Usage: tests/fixtures/media.sh DIR. Test movies covering resolutions, audio
# layouts and languages, each with a poster.
set -euo pipefail

media=$1/movies

# A global LD_LIBRARY_PATH (alsa-lib from the host profile) breaks the flake ffmpeg.
# A fixture never takes more than a few seconds: a stuck encode fails instead of hanging.
# -nostdin: under timeout ffmpeg runs in a background process group, and reading the
# terminal would stop it (SIGTTIN/SIGTTOU) until the timeout kills it.
ff() { timeout 120 env -u LD_LIBRARY_PATH ffmpeg -nostdin -loglevel error -y "$@"; }

movie() { # $1 title, $2 resolution, $3 audio layout, $4 audio language, [$5 version suffix], [$6 audio title], [$7 seconds]
    local dir="$media/$1 (2024)"
    mkdir -p "$dir"
    [[ -f "$dir/$1 (2024)${5:-}.mkv" ]] || ff \
        -f lavfi -i "testsrc=duration=${7:-2}:size=$2:rate=24" \
        -f lavfi -i "anullsrc=channel_layout=$3:sample_rate=48000" -t "${7:-2}" \
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
# Long enough for the video length badge, which skips runtimes under a minute.
movie "JT Long" 320x240 stereo eng "" "" 120
# #10: three audio tracks, the default one is not the first (ffmpeg flags the first by default).
dir="$media/JT Default (2024)"
mkdir -p "$dir"
[[ -f "$dir/JT Default (2024).mkv" ]] || ff -f lavfi -i "testsrc=duration=2:size=1920x1080:rate=24" \
    -f lavfi -i "anullsrc=channel_layout=stereo:sample_rate=48000" -t 2 \
    -map 0:v -map 1:a -map 1:a -map 1:a -c:v libx264 -pix_fmt yuv420p -c:a aac \
    -metadata:s:a:0 language=deu -metadata:s:a:1 language=jpn -metadata:s:a:2 language=eng \
    -disposition:a:0 0 -disposition:a:1 default -disposition:a:2 0 \
    "$dir/JT Default (2024).mkv"
[[ -f "$dir/poster.jpg" ]] || ff -f lavfi -i "testsrc2=size=1000x1500" -frames:v 1 "$dir/poster.jpg"
printf '<?xml version="1.0" encoding="utf-8" standalone="yes"?>\n<movie>\n  <title>JT Default</title>\n  <year>2024</year>\n</movie>\n' \
    >"$dir/JT Default (2024).nfo"
# Two badges in the HDR panel: HDR10 from the HEVC color metadata, 3D from the file name.
dir="$media/JT HDR 3D (2024)"
mkdir -p "$dir"
[[ -f "$dir/JT HDR 3D (2024).3D.HSBS.mkv" ]] || ff -f lavfi -i "testsrc=duration=2:size=1920x1080:rate=24" \
    -f lavfi -i "anullsrc=channel_layout=stereo:sample_rate=48000" -t 2 \
    -c:v libx265 -pix_fmt yuv420p10le -x265-params log-level=error:colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc \
    -c:a aac -metadata:s:a:0 language=eng "$dir/JT HDR 3D (2024).3D.HSBS.mkv"
[[ -f "$dir/poster.jpg" ]] || ff -f lavfi -i "testsrc2=size=1000x1500" -frames:v 1 "$dir/poster.jpg"
printf '<?xml version="1.0" encoding="utf-8" standalone="yes"?>\n<movie>\n  <title>JT HDR 3D</title>\n  <year>2024</year>\n</movie>\n' \
    >"$dir/movie.nfo"
# #6/#7: English audio, English and French subtitles.
dir="$media/JT Subs (2024)"
mkdir -p "$dir"
if [[ ! -f "$dir/JT Subs (2024).mkv" ]]; then
    srt=$(mktemp)
    printf '1\n00:00:00,000 --> 00:00:01,000\nHello\n' >"$srt"
    ff -f lavfi -i "testsrc=duration=2:size=1920x1080:rate=24" \
        -f lavfi -i "anullsrc=channel_layout=stereo:sample_rate=48000" -f srt -i "$srt" \
        -map 0:v -map 1:a -map 2 -map 2 -t 2 -c:v libx264 -pix_fmt yuv420p -c:a aac -c:s srt \
        -metadata:s:a:0 language=eng \
        -metadata:s:s:0 language=eng -metadata:s:s:1 language=fra \
        "$dir/JT Subs (2024).mkv"
    rm -f "$srt"
fi
[[ -f "$dir/poster.jpg" ]] || ff -f lavfi -i "testsrc2=size=1000x1500" -frames:v 1 "$dir/poster.jpg"
printf '<?xml version="1.0" encoding="utf-8" standalone="yes"?>\n<movie>\n  <title>JT Subs</title>\n  <year>2024</year>\n</movie>\n' \
    >"$dir/JT Subs (2024).nfo"
# Episodes take the thumbnail settings; the local "<file>-thumb.jpg" is their primary image.
dir="$1/shows/JT Show/Season 01"
mkdir -p "$dir"
[[ -f "$dir/JT Show S01E01.mkv" ]] || ff -f lavfi -i "testsrc=duration=2:size=1920x1080:rate=24" \
    -f lavfi -i "anullsrc=channel_layout=stereo:sample_rate=48000" -t 2 \
    -c:v libx264 -pix_fmt yuv420p -c:a aac -metadata:s:a:0 language=eng "$dir/JT Show S01E01.mkv"
[[ -f "$dir/JT Show S01E01-thumb.jpg" ]] || ff -f lavfi -i "testsrc2=size=1920x1080" -frames:v 1 "$dir/JT Show S01E01-thumb.jpg"
