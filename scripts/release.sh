#!/bin/sh

VERSION=$1

sed -e "s/VERSION/$1/" -i TwitchShark/modinfo.json
(cd TwitchShark && zip -qq -r TwitchShark.rmod . -x '*.csproj' -x '*.rmod')

