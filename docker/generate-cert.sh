#!/usr/bin/env sh
# Generates a self-signed TLS certificate for the API container.
# The certificate covers the in-network hostname `api` and `localhost`, so both the seed
# container and the host machine can validate it.
#
# Output (docker/https/):
#   senswave.api.pfx - served by Kestrel (ASPNETCORE_Kestrel__Certificates__Default__Path)
#   senswave.api.crt - trusted by the seed container (SSL_CERT_FILE) and by clients
#
# Usage: ./generate-cert.sh [pfx-password]   (default password: password)

set -eu

PASSWORD="${1:-password}"
DIR="$(cd "$(dirname "$0")" && pwd)/https"
CONFIG="$DIR/senswave.api.cnf"

mkdir -p "$DIR"

cat > "$CONFIG" <<'EOF'
[req]
prompt = no
distinguished_name = dn
x509_extensions = ext

[dn]
CN = api

[ext]
basicConstraints = critical, CA:FALSE
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = DNS:api, DNS:localhost, IP:127.0.0.1
EOF

openssl req -x509 -newkey rsa:2048 -sha256 -days 365 -nodes \
    -config "$CONFIG" \
    -keyout "$DIR/senswave.api.key" \
    -out "$DIR/senswave.api.crt"

openssl pkcs12 -export \
    -inkey "$DIR/senswave.api.key" \
    -in "$DIR/senswave.api.crt" \
    -out "$DIR/senswave.api.pfx" \
    -passout "pass:$PASSWORD"

rm -f "$DIR/senswave.api.key" "$CONFIG"

# Readable by the non-root `app` user in the containers.
chmod 644 "$DIR/senswave.api.pfx" "$DIR/senswave.api.crt"

echo "Certificate written to $DIR (valid 365 days, SAN: api, localhost, 127.0.0.1)"
