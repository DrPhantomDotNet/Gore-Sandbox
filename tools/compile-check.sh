#!/bin/bash
# Offline compile check for the game scripts (no Unity needed): builds them against Unity reference DLLs from NuGet
# (UnityEngine.Modules 2021.3, close enough for nearly every API this project uses), once as PC and once as Android.
# The real check is the Unity build in GitHub Actions. Usage: bash tools/compile-check.sh
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
W=/tmp/gscheck
if ! command -v dotnet >/dev/null; then (apt-get update -qq && apt-get install -y -qq dotnet-sdk-8.0) >/dev/null 2>&1; fi
if [ ! -d "$W/pkg/um/lib/net45" ]; then
  mkdir -p "$W/pkg" && curl -s -L -o "$W/pkg/um.nupkg" "https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg"
  (cd "$W/pkg" && unzip -q -o um.nupkg -d um)
fi
make_proj() { # $1 path, $2 defines
  mkdir -p "$(dirname "$1")"
  { echo "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9.0</LangVersion><Nullable>disable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>\$(DefineConstants);$2</DefineConstants><NoWarn>CS0618;CS0414;CS0649;CS0169;CS0067;CS0162;CS0219;CS0108;CS0114</NoWarn><GenerateAssemblyInfo>false</GenerateAssemblyInfo><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup><ItemGroup><Compile Include=\"$ROOT/Assets/**/*.cs\" Exclude=\"$ROOT/Assets/**/Editor/**/*.cs\" /></ItemGroup><ItemGroup>"
    for f in "$W"/pkg/um/lib/net45/*.dll; do n=$(basename "$f" .dll); echo "<Reference Include=\"$n\"><HintPath>$f</HintPath><Private>false</Private></Reference>"; done
    echo "</ItemGroup></Project>"; } > "$1"
}
make_proj "$W/rt/Runtime.csproj" "UNITY_STANDALONE;UNITY_2021_3_OR_NEWER"
make_proj "$W/and/Android.csproj" "UNITY_ANDROID;UNITY_2021_3_OR_NEWER;GS_REFCHECK"
for p in "$W/rt/Runtime.csproj" "$W/and/Android.csproj"; do
  echo "== $(basename "$p")"
  timeout 600 dotnet build -nologo -v q "$p" 2>&1 | grep -E " error |Build succeeded" | sed "s#$ROOT/Assets/SolarEclipse/##; s# \[/tmp.*##" | sort -u | head -25
done
# Unity 6 removed APIs the 2021 reference DLLs still accept: catch them here (Core/RB.cs wraps them behind #if).
bad=$(find "$ROOT/Assets" -name '*.cs' -print0 | xargs -0 awk 'FNR==1{e=0} /#if UNITY_6000/{u=1} /#else/{if(u)e=1} /#endif/{u=0;e=0} /\<PhysicMaterial(Combine)?\>/{if(!e) print FILENAME":"FNR": "$0}')
if [ -n "$bad" ]; then echo "== Unity 6 API check"; echo "$bad" | sed "s#$ROOT/##"; echo " error: PhysicMaterial is PhysicsMaterial in Unity 6; use PhysMat (Core/RB.cs)"; fi
