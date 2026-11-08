#!/usr/bin/env bash
set -euo pipefail

dotnet_channel=${1:?Missing .NET channel}
node_version=${2:?Missing Node version}
pnpm_version=${3:?Missing pnpm version}
aspire_version=${4:?Missing Aspire version}
case "${5:?Missing target architecture}" in
  amd64) architecture=x64 ;;
  arm64) architecture=arm64 ;;
  *)
    echo "Unsupported architecture: $5 (expected amd64 or arm64)" >&2
    exit 1
    ;;
esac

temporary=$(mktemp -d)
archive="node-v${node_version}-linux-${architecture}.tar.xz"
cleanup() {
  rm -f "$temporary/dotnet-install.sh" "$temporary/SHASUMS256.txt" "$temporary/$archive"
  rmdir "$temporary"
}
trap cleanup EXIT

curl --proto '=https' --tlsv1.2 -fsSL https://dot.net/v1/dotnet-install.sh \
  -o "$temporary/dotnet-install.sh"
bash "$temporary/dotnet-install.sh" --channel "$dotnet_channel" --quality ga \
  --architecture "$architecture" --install-dir /usr/share/dotnet --no-path
ln -sfn /usr/share/dotnet/dotnet /usr/local/bin/dotnet

curl --proto '=https' --tlsv1.2 -fsSL "https://nodejs.org/dist/v${node_version}/SHASUMS256.txt" \
  -o "$temporary/SHASUMS256.txt"
curl --proto '=https' --tlsv1.2 -fsSL "https://nodejs.org/dist/v${node_version}/${archive}" \
  -o "$temporary/$archive"
(
  cd "$temporary"
  grep -E "^[0-9a-f]{64}  ${archive//./\\.}$" SHASUMS256.txt | sha256sum --check --strict -
)
mkdir -p /usr/local/share/collect-play-achieve/node
tar --no-same-owner --strip-components=1 -xJf "$temporary/$archive" \
  -C /usr/local/share/collect-play-achieve/node
for executable in node npm npx; do
  ln -sfn "/usr/local/share/collect-play-achieve/node/bin/$executable" "/usr/local/bin/$executable"
done

npm install --global --prefix /usr/local "pnpm@$pnpm_version"
dotnet tool install Aspire.Cli --version "$aspire_version" \
  --tool-path /usr/local/share/collect-play-achieve/aspire
ln -sfn /usr/local/share/collect-play-achieve/aspire/aspire /usr/local/bin/aspire
aspire setup --non-interactive
chmod -R a+rX /usr/local/share/collect-play-achieve/aspire
chown -R 1000:1000 /usr/local/share/collect-play-achieve/aspire

dotnet --info
test "$(node --version)" = "v$node_version"
test "$(pnpm --version)" = "$pnpm_version"
aspire --version
