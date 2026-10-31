"""Publish a built Dalamud package into this checkout's custom repository."""
import argparse
import json
import shutil
import time
import zipfile
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--package-dir', type=Path, required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
manifest = json.loads((args.package_dir / 'Dagobert.json').read_text(encoding='utf-8-sig'))
archive = args.package_dir / 'latest.zip'
with zipfile.ZipFile(archive) as package:
    if package.testzip() is not None:
        raise ValueError('Plugin ZIP is corrupt')
    embedded = json.loads(package.read('Dagobert.json'))
    if embedded['AssemblyVersion'] != manifest['AssemblyVersion']:
        raise ValueError('Package and manifest versions differ')
    for dependency in ('Dagobert.dll', 'ECommons.dll', 'System.Speech.dll'):
        if dependency not in package.namelist():
            raise ValueError(f'Missing packaged dependency: {dependency}')

index = root / 'pluginmaster.json'
if index.exists():
    current = json.loads(index.read_text())[0]
    if tuple(map(int, current['AssemblyVersion'].split('.'))) > tuple(map(int, manifest['AssemblyVersion'].split('.'))):
        raise ValueError('Refusing to replace a newer plugin version')

# A versioned path keeps package and index updates consistent across CDN caches.
destination = root / 'plugins' / 'Dagobert' / manifest['AssemblyVersion'] / 'latest.zip'
destination.parent.mkdir(parents=True, exist_ok=True)
if destination.exists() and destination.read_bytes() != archive.read_bytes():
    raise ValueError('Bump the plugin version before replacing a published package')
shutil.copyfile(archive, destination)
url = 'https://raw.githubusercontent.com/Isaiahfeldt/Dagobert/master/' + destination.relative_to(root).as_posix()
manifest.update(DownloadLinkInstall=url, DownloadLinkUpdate=url, DownloadLinkTesting=url,
                IsHide=False, IsTestingExclusive=False, LastUpdate=str(int(time.time())))
index.write_text(json.dumps([manifest], indent=2) + '\n', encoding='utf-8')
print(f"Published Dagobert {manifest['AssemblyVersion']}: {url}")
