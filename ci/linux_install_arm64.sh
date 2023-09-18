#!/bin/bash

mkdir -p package/arm64/DEBIAN
mkdir -p package/arm64/usr/local/bin/Aporta
cp setup/Linux/control-arm64 package/arm64/DEBIAN/control

cp -r src/Aporta/bin/Release/net7.0/linux-arm64/publish/* package/arm64/usr/local/bin/Aporta

dpkg-deb --build package/arm64 "$1/Aporta.linux-arm64.deb"