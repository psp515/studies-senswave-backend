#!/usr/bin/env sh
# Local phase only: self-signed TLS certificate for the Mosquitto stand-in (SAN: mqtt, localhost).
# The API always connects to brokers with TLS (the seed sets UseTls=true), so the local broker needs
# a TLS listener the API trusts. Output goes next to the API certificate (CERT_DIR, default docker/https):
#   mqtt.crt - trusted by the API containers (API_SSL_CERT_FILE=/https/mqtt.crt)
#   mqtt.key - served by Mosquitto
# Usage: ./generate-cert.sh [cert-dir]
set -eu
export MSYS_NO_PATHCONV=1

DIR="${1:-$(cd "$(dirname "$0")/../../.." && pwd)/docker/https}"
mkdir -p "$DIR"

# Relative names: a native Windows openssl does not understand MSYS paths like /e/...
cd "$DIR"
openssl req -x509 -newkey rsa:2048 -sha256 -days 365 -nodes \
    -subj "/CN=mqtt" \
    -addext "subjectAltName=DNS:mqtt,DNS:localhost,IP:127.0.0.1" \
    -addext "basicConstraints=critical,CA:FALSE" \
    -addext "extendedKeyUsage=serverAuth" \
    -keyout mqtt.key -out mqtt.crt

chmod 644 mqtt.key mqtt.crt
echo "Certificate written to $DIR (valid 365 days, SAN: mqtt, localhost, 127.0.0.1)"
