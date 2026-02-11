#! /usr/bin/env bash
#set -uvx
set -e
cd "$(dirname "$0")"
cwd=`pwd`
ts=`date "+%Y.%m%d.%H%M.%S"`

cd $cwd
version=`cat version.txt`
echo \$version=$version

rm -rf *.nupkg
dotnet pack -o . -p:Configuration=Release

tag="v$version"
cd $cwd
git add .
git commit -m"$tag"
git tag -a "$tag" -m"$tag"
git push origin "$tag"
git push origin HEAD:main -f
git remote -v
