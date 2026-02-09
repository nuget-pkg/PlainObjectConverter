#! /usr/bin/env bash
#set -uvx
set -e
cd "$(dirname "$0")"
cwd=`pwd`
ts=`date "+%Y.%m%d.%H%M.%S"`
version="${ts}"

current=$(cd $(dirname $0);pwd)
echo $current
name=`echo "$current" | sed -e 's/.*\/\([^\/]*\)$/\1/'`
echo $name

cd $cwd/$name
sed -i -e "s/<Version>.*<\/Version>/<Version>${version}<\/Version>/g" $name.csproj
cd $cwd/
echo ${version}>version.txt
