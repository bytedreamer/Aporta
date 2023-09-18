#!/bin/bash

mkdir -p "$1/package/amd64/DEBIAN"
mkdir -p "$1/package/amd64/usr/local/bin/Aporta"
cp "$1/setup/Linux/control-amd64" "$1/package/amd64/DEBIAN/control"

echo "$1/src/Aporta/bin/Release/net7.0/linux-arm64/publish"
ls "$1/src/Aporta/bin/Release/net7.0/linux-arm64/publish"

cp -r "$1/src/Aporta/bin/Release/net7.0/linux-x64/publish/*" "$1/package/amd64/usr/local/bin/Aporta"

dpkg-deb --build "$1/package/amd64" "$2/Aporta.linux-amd64.deb"