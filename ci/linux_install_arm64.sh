#!/bin/bash

mkdir -p "$1/package/arm64/DEBIAN"
mkdir -p "$1/package/arm64/opt/Aporta"
cp "$1/setup/Linux/control-arm64" "$1/package/arm64/DEBIAN/control"

cp -RT "$1/src/Aporta/bin/Release/net8.0/linux-arm64/publish/" "$1/package/arm64/opt/Aporta/"

dpkg-deb -Zgzip -b  "$1/package/arm64" "$2/Aporta.linux-arm64.deb"