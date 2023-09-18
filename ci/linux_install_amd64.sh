#!/bin/bash

mkdir -p package/amd64/DEBIAN
mkdir -p package/amd64/usr/local/bin/Aporta
cp setup/Linux/control-amd64 package/amd64/DEBIAN/control

cp -r src/Aporta/bin/Release/net7.0/linux-x64/publish/* package/amd64/usr/local/bin/Aporta

dpkg-deb --build package/amd64 "$1/Aporta.linux-amd64.deb"