#!/bin/bash

mkdir -p "$1/package/armhf/DEBIAN"
mkdir -p "$1/package/armhf/usr/local/bin/Aporta"
cp "$1/setup/Linux/control-armhf" "$1/package/armhf/DEBIAN/control"

cp -R "$1/src/Aporta/bin/Release/net7.0/linux-arm/publish" "$1/package/armhf/usr/local/bin/Aporta"

dpkg-deb --build "$1/package/armhf" "$2/Aporta.linux-armhf.deb"