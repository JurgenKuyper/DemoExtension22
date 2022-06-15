#!/bin/bash

# make a temp staging folder for the .yip archive and copy only the files we need on the pendant
rm -Rf /tmp/DemoExtension22
rm DemoExtension22.yip
mkdir -p /tmp/DemoExtension22
mkdir -p /tmp/DemoExtension22/images
mkdir -p /tmp/DemoExtension22/help
mkdir -p /tmp/DemoExtension22/jobs
cp bin/Debug/netcoreapp2.2/images/* /tmp/DemoExtension22/images/
cp -r bin/Debug/netcoreapp2.2/help/* /tmp/DemoExtension22/help/
cp -r bin/Debug/netcoreapp2.2/jobs/* /tmp/DemoExtension22/jobs/
cp -r bin/Debug/netcoreapp2.2/*.yml bin/Debug/netcoreapp2.2/linux-arm/publish/* /tmp/DemoExtension22/

# Finally, ask Smart Packager to create a unprotected package using the JSONNET template & the temp folder as archive .yip content
SmartPackager --unprotected --package DemoExtension22.yip --new demo-extension-yip-template.jsonnet --archive /tmp/DemoExtension22
