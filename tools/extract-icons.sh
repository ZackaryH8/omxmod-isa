#!/usr/bin/env bash
# Reproduce reference images; the source PDF and output are not MIT project assets.
set -euo pipefail

isa_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
command -v pdftoppm >/dev/null || { echo 'Install Poppler (pdftoppm) first.' >&2; exit 1; }
isa_temp="$(mktemp -d)"
trap 'rm -rf "$isa_temp"' EXIT
isa_pdf="${1:-}"
if [[ -z "$isa_pdf" ]]; then
  command -v curl >/dev/null || { echo 'Install curl or pass the path to a local PDF.' >&2; exit 1; }
  isa_pdf="$isa_temp/reference.pdf"
  curl --fail --location --silent --show-error --proto '=https' \
    'https://foi.tfl.gov.uk/FOI-1516-2627/Bus%20Vehicle%20Specification_v2.7_ISA.pdf' \
    --output "$isa_pdf"
fi

# The fixed crops are tied to this exact reference, not arbitrary future PDF revisions.
isa_expected='76c52e3c7105f1f851eb671f658e11baee9f4ac0319f0ec885acedd9c8d914c8'
isa_actual="$(openssl dgst -sha256 "$isa_pdf")"
[[ "${isa_actual##* }" == "$isa_expected" ]] || { echo 'Reference PDF checksum mismatch; extraction stopped.' >&2; exit 1; }
mkdir -p "$isa_root/assets"
pdftoppm -f 4 -l 4 -r 144 -x 156 -y 184 -W 225 -H 169 -png -singlefile "$isa_pdf" "$isa_temp/isa-amber"
pdftoppm -f 4 -l 4 -r 144 -x 457 -y 185 -W 224 -H 169 -png -singlefile "$isa_pdf" "$isa_temp/isa-green"
pdftoppm -f 4 -l 4 -r 144 -x 758 -y 185 -W 225 -H 169 -png -singlefile "$isa_pdf" "$isa_temp/isa-white"
cp "$isa_temp"/isa-*.png "$isa_root/assets/"
echo 'Reference icons extracted for local use. See assets/README.md for provenance and terms.'
