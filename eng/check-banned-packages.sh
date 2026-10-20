#!/usr/bin/env bash
# Pada ako su u Directory.Packages.props ili *.csproj zabranjene biblioteke (plan COA-14, O9).
set -euo pipefail
cd "$(dirname "$0")/.."

files=$(git ls-files 'Directory.Packages.props' 'Directory.Build.props' '*.csproj' | tr '\n' ' ')
fail=0

# Zabranjeni paketi (bilo koja verzija); poklapa se ceo naziv paketa ili njegovi pod-paketi (npr. MediatR.Extensions.*).
banned='EPPlus|AutoMapper|MediatR|Moq'
# shellcheck disable=SC2086
if grep -nEi "<Package(Version|Reference)[^>]*Include=\"($banned)(\.[A-Za-z0-9.]+)?\"" $files; then
  echo "::error::Zabranjena biblioteka (EPPlus, AutoMapper, MediatR, Moq) pronadjena u gornjim fajlovima."
  fail=1
fi

# FluentAssertions: dozvoljeno samo < 8 (licenca).
# shellcheck disable=SC2086
while IFS= read -r line; do
  [ -z "$line" ] && continue
  ver=$(sed -nE 's/.*Version="([0-9]+)[^"]*".*/\1/p' <<<"$line")
  if [ -z "$ver" ] || [ "$ver" -ge 8 ]; then
    echo "::error::FluentAssertions 8+ je zabranjen: $line"
    fail=1
  fi
done < <(grep -hEi '<Package(Version|Reference)[^>]*Include="FluentAssertions"' $files || true)

[ "$fail" -eq 0 ] && echo "OK: nema zabranjenih biblioteka."
exit "$fail"
