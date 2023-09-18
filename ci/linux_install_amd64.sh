#!/bin/bash

mkdir -p "$1/package/amd64/DEBIAN"
mkdir -p "$1/package/amd64/opt/Aporta"
cp "$1/setup/Linux/control-amd64" "$1/package/amd64/DEBIAN/control"

cp -RT "$1/src/Aporta/bin/Release/net7.0/linux-x64/publish/" "$1/package/amd64/opt/Aporta/"

dpkg-deb -b "$1/package/amd64" -Zgzip "$2/Aporta.linux-amd64.deb"