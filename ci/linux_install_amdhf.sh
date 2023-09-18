#!/bin/bash

mkdir -p package/armhf/DEBIAN
mkdir -p package/armhf/usr/local/bin/Aporta
cp setup/Linux/control-armhf package/armhf/DEBIAN/control

cp -r src/Aporta/bin/Release/net7.0/linux-arm/publish/* package/armhf/usr/local/bin/Aporta

dpkg-deb --build package/armhf "$1/Aporta.linux-armhf.deb"