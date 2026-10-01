#!/usr/bin/env python3
"""Build, test, and package portable mod DLLs without bundling the game or SDK."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--dotnet', default='dotnet', help='Path to a .NET SDK executable')
parser.add_argument('--game-path', help='Directory containing StardewModdingAPI.dll')
args = parser.parse_args()
env = os.environ.copy()
env.update(DOTNET_CLI_HOME=str(ROOT / '.cache' / 'dotnet'), DOTNET_CLI_TELEMETRY_OPTOUT='1',
           DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_GENERATE_ASPNET_CERTIFICATE='false',
           DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='true', MSBuildEnableWorkloadResolver='false',
           DOTNET_CLI_UI_LANGUAGE='en-US')

def run(*arguments):
    subprocess.run([args.dotnet, *arguments], cwd=ROOT, env=env, check=True)

run('restore', 'tests/BirthdayPlanner.Tests/BirthdayPlanner.Tests.csproj', '--disable-parallel')
run('run', '--project', 'tests/BirthdayPlanner.Tests/BirthdayPlanner.Tests.csproj', '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false')
restore_args = ['restore', 'src/BirthdayPlanner/BirthdayPlanner.csproj', '--disable-parallel']
build_args = ['build', 'src/BirthdayPlanner/BirthdayPlanner.csproj', '-c', 'Release', '--no-restore', '--nologo', '-p:UseSharedCompilation=false']
if args.game_path:
    restore_args.append('-p:GamePath=' + str(Path(args.game_path).resolve()))
    build_args.append('-p:GamePath=' + str(Path(args.game_path).resolve()))
run(*restore_args)
run(*build_args)

manifest = json.loads((ROOT / 'manifest.json').read_text())
package = ROOT / 'dist' / 'BirthdayPlanner'
package.mkdir(parents=True, exist_ok=True)
output = ROOT / 'src' / 'BirthdayPlanner' / 'bin' / 'Release' / 'net6.0'
for name in ['BirthdayPlanner.dll', 'BirthdayPlanner.Core.dll', 'manifest.json']:
    shutil.copy2(output / name, package / name)
shutil.copytree(ROOT / 'i18n', package / 'i18n', dirs_exist_ok=True)
for name in ['LICENSE', 'THIRD_PARTY_NOTICES.md', 'INSTALL.txt']:
    shutil.copy2(ROOT / name, package / name)
archive = ROOT / 'dist' / f'BirthdayPlanner-{manifest["Version"]}.zip'
allowed = ['BirthdayPlanner.dll', 'BirthdayPlanner.Core.dll', 'manifest.json', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'INSTALL.txt']
files = [package / name for name in allowed] + sorted((package / 'i18n').glob('*.json'))
with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED) as target:
    for path in files:
        target.write(path, 'BirthdayPlanner/' + path.relative_to(package).as_posix())
with zipfile.ZipFile(archive) as target:
    assert target.testzip() is None, 'ZIP integrity check failed'
    assert 'BirthdayPlanner/BirthdayPlanner.dll' in target.namelist()
    assert not any(name.endswith('.cs') or '/test-session/' in name for name in target.namelist())
hashes = {path.relative_to(package).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in files}
(ROOT / 'dist' / 'sha256.json').write_text(json.dumps(hashes, indent=2) + '\n')
print('Packaged:', archive)
