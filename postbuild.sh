#!/usr/bin/env bash
set -euo pipefail

zip "release.zip" -rMM "BepInEx"
zip "release.zip" -jMM "$HOME/.nuget/packages/luacsharp/0.5.6/lib/netstandard2.1/Lua.dll" "$HOME/.nuget/packages/luacsharp.annotations/0.5.6/lib/netstandard2.1/Lua.Annotations.dll" "$HOME/.nuget/packages/microsoft.bcl.timeprovider/8.0.0/lib/netstandard2.0/Microsoft.Bcl.TimeProvider.dll" "$HOME/.nuget/packages/system.runtime.compilerservices.unsafe/6.0.0/lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll"
