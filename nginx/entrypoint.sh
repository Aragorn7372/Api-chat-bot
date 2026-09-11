#!/bin/sh
set -e

CERT_DIR=/etc/nginx/ssl
mkdir -p "$CERT_DIR"

#  1. Generar certificado (Cloudflare Origin CA o self-signed)
if [ ! -f "$CERT_DIR/cert.pem" ] || [ ! -f "$CERT_DIR/key.pem" ]; then
    echo "Generating certificate..."

    if [ -n "$CF_API_TOKEN" ] && [ -n "$CF_DOMAIN" ]; then
        # ── Cloudflare Origin CA ──
        echo "  Using Cloudflare Origin CA for $CF_DOMAIN"

        openssl req -new -newkey rsa:2048 -nodes \
            -keyout "$CERT_DIR/key.pem" \
            -out "$CERT_DIR/csr.pem" \
            -subj "/CN=$CF_DOMAIN"

        CSR=$(awk '{printf "%s\\n", $0}' "$CERT_DIR/csr.pem" | tr -d '\n')

        RESPONSE=$(curl -s -X POST "https://api.cloudflare.com/client/v4/certificates" \
            -H "Authorization: Bearer $CF_API_TOKEN" \
            -H "Content-Type: application/json" \
            -d "{
                \"hostnames\": [\"$CF_DOMAIN\"],
                \"request_type\": \"origin-rsa\",
                \"csr\": \"$CSR\",
                \"requested_validity\": 15
            }")

        SUCCESS=$(echo "$RESPONSE" | jq -r '.success')

        if [ "$SUCCESS" = "true" ]; then
            echo "$RESPONSE" | jq -r '.result.certificate' > "$CERT_DIR/cert.pem"
            curl -sfSL https://developers.cloudflare.com/ssl/static/origin_ca_rsa_root.pem -o "$CERT_DIR/cloudflare_ca.pem"
            echo "  Cloudflare Origin CA certificate issued"
        else
            echo "  ERROR: Cloudflare API failed. Check CF_API_TOKEN and CF_DOMAIN."
            echo "$RESPONSE" | jq -r '.errors'
            exit 1
        fi

        rm -f "$CERT_DIR/csr.pem"
    else
        echo "  Using self-signed certificate (set CF_API_TOKEN + CF_DOMAIN for Cloudflare Origin CA)"
        openssl req -x509 -nodes -days 3650 \
            -newkey rsa:2048 \
            -keyout "$CERT_DIR/key.pem" \
            -out "$CERT_DIR/cert.pem" \
            -subj "/C=XX/ST=NA/L=NA/O=ApiChatbot/CN=localhost"
    fi
fi

#  2. Arrancar nginx
exec nginx -g "daemon off;"
