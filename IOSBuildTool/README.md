# iOS Build Tool

This directory is a repository-level tool that sits alongside `Tools/`.

## What It Does

`build_ios.py` runs the iOS packaging flow in three steps:

1. Call Unity in batchmode and export an Xcode project.
2. Run `xcodebuild archive`.
3. Run `xcodebuild -exportArchive` and export an IPA.

## Files

- `build_ios.py`: command-line entry point
- `Client/Assets/Editor/IOSBuildCommandLine.cs`: Unity batchmode export method

## Basic Usage

Export an Xcode project only:

```bash
python3 IOSBuildTool/build_ios.py \
  --clean \
  --skip-archive \
  --skip-export
```

Build an archive and IPA with automatic signing:

```bash
python3 IOSBuildTool/build_ios.py \
  --clean \
  --team-id 3ZFMN73SWB \
  --bundle-id com.zion.brickblast \
  --export-method development
```

## Common Options

- `--unity`: specify the Unity executable path. If omitted, the tool reads `Client/ProjectSettings/ProjectVersion.txt` and tries to find a matching editor under `/Applications/Unity` or `/Applications/Unity/Hub/Editor`.
- `--team-id`: Apple development team id passed to `xcodebuild`.
- `--bundle-id`: override the iOS bundle identifier for this build only.
- `--bundle-version`: override `CFBundleShortVersionString` for this build only.
- `--build-number`: override `CFBundleVersion` for this build only.
- `--development`: build a Unity development export.
- `--allow-debugging`: enable Unity managed debugging in the exported Xcode project.
- `--skip-unity`: reuse an existing Xcode export.
- `--skip-archive`: stop after exporting the Xcode project.
- `--skip-export`: stop after creating the archive.

## Output Layout

By default outputs are written under:

```text
Builds/iOS/
```

Including:

- `Builds/iOS/Xcode`
- `Builds/iOS/BrickBlast.xcarchive`
- `Builds/iOS/IPA`
- `Builds/iOS/unity_build.log`
