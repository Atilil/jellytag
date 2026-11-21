set shell := ["bash", "-euo", "pipefail", "-c"]

project := "Jellytag/Jellyfin.Plugin.JellyTag"
plugin_guid := "f4a2e8c1-9b3d-4f7a-b6c5-2d8e1a3f9b04"
publish_dir := "artifacts/publish"

jellyfin_image := "docker.io/jellyfin/jellyfin:12.4.20261026-211555"
dev_dir := justfile_directory() / ".dev"
dev_port := env("JELLYTAG_DEV_PORT", "8097")
dev_container := "jellytag-dev"
dev_plugin_dir := dev_dir / "config/plugins/JellyTag"

# List the recipes
default:
    @just --list

# Compile the plugin and its bundled libraries (Release) into artifacts/publish
build:
    dotnet publish -c Release {{ project }} -o {{ publish_dir }}

# Check that tool versions pinned outside the flake match what the flake provides
check-pins:
    #!/usr/bin/env bash
    set -euo pipefail
    flake=$(nix eval --inputs-from . --raw nixpkgs#dotnet-sdk_10.version)
    pinned=$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' global.json)
    if [[ "$flake" == "$pinned" ]]; then
      echo "ok    dotnet SDK $flake"
    else
      echo "DRIFT dotnet SDK: flake provides $flake, global.json says $pinned"
      exit 1
    fi

# Start the local Jellyfin test instance
dev-up:
    #!/usr/bin/env bash
    set -euo pipefail
    mkdir -p "{{ dev_dir }}"/{config,cache,media} "{{ dev_plugin_dir }}"
    cat > "{{ dev_plugin_dir }}/meta.json" <<'EOF'
    {
      "guid": "{{ plugin_guid }}",
      "name": "JellyTag",
      "version": "0.0.0.0",
      "targetAbi": "12.0.0.0",
      "owner": "Atilili",
      "category": "General",
      "description": "Local development build."
    }
    EOF
    podman run -d --replace --name "{{ dev_container }}" \
      -p "{{ dev_port }}:8096" \
      -v "{{ dev_dir }}/config:/config:Z" \
      -v "{{ dev_dir }}/cache:/cache:Z" \
      -v "{{ dev_dir }}/media:/media:Z" \
      "{{ jellyfin_image }}" >/dev/null
    echo "Jellyfin: http://localhost:{{ dev_port }}"

# Build, install the plugin into the local instance and restart it
dev-deploy: build
    #!/usr/bin/env bash
    set -euo pipefail
    since=$(date -u +%Y-%m-%dT%H:%M:%SZ)
    # Stop before copying: Jellyfin reads the loaded DLLs from disk, overwriting them live
    # makes the old plugin crash on shutdown ("Bad IL range").
    # `podman restart` races rootless pasta for the host port: stop, then retry start.
    podman stop "{{ dev_container }}" >/dev/null
    cp "{{ publish_dir }}"/*.dll "{{ publish_dir }}"/*.pdb "{{ dev_plugin_dir }}/"
    for _ in $(seq 10); do podman start "{{ dev_container }}" >/dev/null 2>&1 && break; sleep 1; done
    timeout 180 bash -c 'until curl -sf http://localhost:{{ dev_port }}/health >/dev/null; do sleep 2; done'
    podman logs --since "$since" "{{ dev_container }}" 2>&1 | grep -E 'JellyTag|\[ERR\]' || true

# Follow the JellyTag lines of the Jellyfin log
dev-logs:
    podman logs -f "{{ dev_container }}" 2>&1 | grep --line-buffered -E 'JellyTag|\[ERR\]'

# Stop the local instance (data in .dev/ is kept)
dev-down:
    podman stop "{{ dev_container }}"
