#!/bin/sh

VERSION=$1

echo "Replacing version with ${VERSION}"
sed -e "s/VERSION/${VERSION}/" -i TwitchShark/modinfo.json
sed -e "s/VERSION/${VERSION}/" -i TwitchShark/TwitchShark.cs
cd TwitchShark
zip -qq -r ../TwitchShark.rmod . -x '*.csproj' -x '*.rmod'

