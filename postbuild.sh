#!/usr/bin/env bash
set -euo pipefail

addzip() {
  zip "$1" -rMM "BepInEx"
  zip "$1" -jMM "$HOME/.nuget/packages/luacsharp/0.5.6/lib/netstandard2.1/Lua.dll" "$HOME/.nuget/packages/luacsharp.annotations/0.5.6/lib/netstandard2.1/Lua.Annotations.dll" "$HOME/.nuget/packages/microsoft.bcl.timeprovider/8.0.0/lib/netstandard2.0/Microsoft.Bcl.TimeProvider.dll" "$HOME/.nuget/packages/system.runtime.compilerservices.unsafe/6.0.0/lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll"
}

echo "Adding dll files to release zip..."
addzip release.zip

echo -e "\e[1;94m****DEBUG BUILD****\e[0m"
dotnet build -c Debug -o build-debug
echo -e "\e[1;94m**** DEBUG ZIP ****\e[0m"
zip "release-debug.zip" -jMM "manifest.json" build-debug/**.dll
set +e
zip "release-debug.zip" -j "icon.png" "README.md"
_e="$?"
if [ "$_e" != 0 ] && [ "$_e" != 12 ]; then exit "$_e"; fi
unset _e
set -e
echo "Adding dll files to debug zip..."
addzip release-debug.zip
mv -v release-debug.zip release
