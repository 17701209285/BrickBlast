#!/usr/bin/env python3
from __future__ import annotations

import argparse
import plistlib
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path


def parse_args() -> argparse.Namespace:
    root_dir = Path(__file__).resolve().parents[1]
    default_project = root_dir / "Client"
    default_build_root = root_dir / "Builds" / "iOS"

    parser = argparse.ArgumentParser(description="BrickBlast iOS build tool")
    parser.add_argument("--project", default=str(default_project), help="Unity project path")
    parser.add_argument("--unity", default="", help="Unity executable path")
    parser.add_argument("--build-root", default=str(default_build_root), help="Build root directory")
    parser.add_argument("--xcode-output", default="", help="Exported Xcode project directory")
    parser.add_argument("--archive-path", default="", help="xcarchive output path")
    parser.add_argument("--ipa-output", default="", help="IPA export directory")
    parser.add_argument("--unity-log-file", default="", help="Unity log file path")
    parser.add_argument("--scheme", default="Unity-iPhone", help="Xcode scheme")
    parser.add_argument("--configuration", default="Release", help="Xcode build configuration")
    parser.add_argument("--export-method", default="development", help="xcodebuild export method")
    parser.add_argument("--signing-style", default="automatic", help="xcodebuild signing style")
    parser.add_argument("--team-id", default="", help="Apple development team id")
    parser.add_argument("--bundle-id", default="", help="Override bundle identifier")
    parser.add_argument("--bundle-version", default="", help="Override CFBundleShortVersionString")
    parser.add_argument("--build-number", default="", help="Override CFBundleVersion")
    parser.add_argument("--development", action="store_true", help="Export a Unity development build")
    parser.add_argument("--allow-debugging", action="store_true", help="Enable managed debugging in Unity build")
    parser.add_argument("--clean", action="store_true", help="Delete existing build outputs before building")
    parser.add_argument("--skip-unity", action="store_true", help="Skip Unity Xcode export step")
    parser.add_argument("--skip-archive", action="store_true", help="Skip xcodebuild archive step")
    parser.add_argument("--skip-export", action="store_true", help="Skip xcodebuild -exportArchive step")
    return parser.parse_args()


def read_project_version(project_dir: Path) -> str:
    version_file = project_dir / "ProjectSettings" / "ProjectVersion.txt"
    for line in version_file.read_text(encoding="utf-8").splitlines():
        if line.startswith("m_EditorVersion:"):
            return line.split(":", 1)[1].strip()
    raise RuntimeError(f"Failed to read Unity version from {version_file}")


def discover_unity(project_dir: Path) -> Path:
    version = read_project_version(project_dir)
    candidates = [
        Path("/Applications/Unity") / f"Unity-{version}" / "Unity.app" / "Contents" / "MacOS" / "Unity",
        Path("/Applications/Unity/Hub/Editor") / version / "Unity.app" / "Contents" / "MacOS" / "Unity",
    ]
    for candidate in candidates:
        if candidate.exists():
            return candidate
    raise RuntimeError(f"Unity {version} not found. Use --unity to specify the executable path.")


def ensure_deleted(path: Path) -> None:
    if path.is_dir():
        shutil.rmtree(path)
    elif path.exists():
        path.unlink()


def run(cmd: list[str], cwd: Path | None = None) -> None:
    print("+", " ".join(str(part) for part in cmd))
    subprocess.run(cmd, cwd=str(cwd) if cwd else None, check=True)


def build_unity_xcode_project(
    unity_path: Path,
    project_dir: Path,
    xcode_output: Path,
    unity_log_file: Path,
    args: argparse.Namespace,
) -> None:
    command = [
        str(unity_path),
        "-batchmode",
        "-quit",
        "-projectPath",
        str(project_dir),
        "-logFile",
        str(unity_log_file),
        "-executeMethod",
        "BrickBlast.Editor.IOSBuildCommandLine.BuildFromCommandLine",
        "--ios-output-path",
        str(xcode_output),
    ]

    if args.clean:
        command.append("--ios-clean-output")
    if args.bundle_id:
        command.extend(["--ios-bundle-id", args.bundle_id])
    if args.bundle_version:
        command.extend(["--ios-bundle-version", args.bundle_version])
    if args.build_number:
        command.extend(["--ios-build-number", args.build_number])
    if args.development:
        command.append("--ios-development")
    if args.allow_debugging:
        command.append("--ios-allow-debugging")

    run(command, cwd=project_dir)


def create_export_options(path: Path, args: argparse.Namespace) -> None:
    export_options = {
        "method": args.export_method,
        "signingStyle": args.signing_style.lower(),
        "destination": "export",
        "stripSwiftSymbols": True,
        "compileBitcode": False,
        "manageAppVersionAndBuildNumber": False,
    }

    if args.team_id:
        export_options["teamID"] = args.team_id

    with path.open("wb") as file:
        plistlib.dump(export_options, file)


def archive_xcode_project(
    xcode_container: Path,
    archive_path: Path,
    args: argparse.Namespace,
) -> None:
    command = ["xcodebuild"]
    if xcode_container.suffix == ".xcworkspace":
        command.extend(["-workspace", str(xcode_container)])
    else:
        command.extend(["-project", str(xcode_container)])

    command.extend(
        [
            "-scheme",
            args.scheme,
            "-configuration",
            args.configuration,
            "-destination",
            "generic/platform=iOS",
            "-archivePath",
            str(archive_path),
            "archive",
        ]
    )

    if args.team_id:
        command.extend(["DEVELOPMENT_TEAM=" + args.team_id, "CODE_SIGN_STYLE=" + args.signing_style.capitalize()])
        command.append("-allowProvisioningUpdates")
    if args.bundle_id:
        command.append("PRODUCT_BUNDLE_IDENTIFIER=" + args.bundle_id)
    if args.bundle_version:
        command.append("MARKETING_VERSION=" + args.bundle_version)
    if args.build_number:
        command.append("CURRENT_PROJECT_VERSION=" + args.build_number)

    run(command, cwd=xcode_container.parent)


def resolve_xcode_container(xcode_output: Path, scheme: str) -> Path:
    preferred_workspace = xcode_output / f"{scheme}.xcworkspace"
    if preferred_workspace.exists():
        return preferred_workspace

    preferred_project = xcode_output / f"{scheme}.xcodeproj"
    if preferred_project.exists():
        return preferred_project

    workspaces = sorted(xcode_output.glob("*.xcworkspace"))
    if workspaces:
        return workspaces[0]

    projects = sorted(xcode_output.glob("*.xcodeproj"))
    if projects:
        return projects[0]

    raise RuntimeError(f"No Xcode project or workspace found under {xcode_output}")


def export_ipa(archive_path: Path, ipa_output: Path, args: argparse.Namespace) -> None:
    ipa_output.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(prefix="ios_export_options_", suffix=".plist", delete=False) as temp_file:
        export_plist = Path(temp_file.name)

    try:
        create_export_options(export_plist, args)
        command = [
            "xcodebuild",
            "-exportArchive",
            "-archivePath",
            str(archive_path),
            "-exportPath",
            str(ipa_output),
            "-exportOptionsPlist",
            str(export_plist),
        ]
        if args.team_id:
            command.append("-allowProvisioningUpdates")
        run(command)
    finally:
        export_plist.unlink(missing_ok=True)


def main() -> int:
    args = parse_args()
    project_dir = Path(args.project).resolve()
    build_root = Path(args.build_root).resolve()
    xcode_output = Path(args.xcode_output).resolve() if args.xcode_output else build_root / "Xcode"
    archive_path = Path(args.archive_path).resolve() if args.archive_path else build_root / "BrickBlast.xcarchive"
    ipa_output = Path(args.ipa_output).resolve() if args.ipa_output else build_root / "IPA"
    unity_log_file = Path(args.unity_log_file).resolve() if args.unity_log_file else build_root / "unity_build.log"
    unity_path = Path(args.unity).resolve() if args.unity else discover_unity(project_dir)

    if args.skip_archive and not args.skip_export:
        raise RuntimeError("--skip-export is required when --skip-archive is set.")

    build_root.mkdir(parents=True, exist_ok=True)
    unity_log_file.parent.mkdir(parents=True, exist_ok=True)

    if args.clean:
        if not args.skip_unity:
            ensure_deleted(xcode_output)
        if not args.skip_archive:
            ensure_deleted(archive_path)
        if not args.skip_export:
            ensure_deleted(ipa_output)

    if not args.skip_unity:
        build_unity_xcode_project(unity_path, project_dir, xcode_output, unity_log_file, args)

    if not args.skip_archive:
        xcode_container = resolve_xcode_container(xcode_output, args.scheme)
        archive_xcode_project(xcode_container, archive_path, args)

    if not args.skip_export:
        if not archive_path.exists():
            raise RuntimeError(f"Archive not found: {archive_path}")
        export_ipa(archive_path, ipa_output, args)

    print("")
    print("Build completed.")
    print(f"Unity:   {unity_path}")
    print(f"Project: {project_dir}")
    print(f"Xcode:   {xcode_output}")
    if not args.skip_archive:
        print(f"Archive: {archive_path}")
    if not args.skip_export:
        print(f"IPA:     {ipa_output}")
    print(f"Log:     {unity_log_file}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except subprocess.CalledProcessError as error:
        print(f"Command failed with exit code {error.returncode}", file=sys.stderr)
        raise
    except Exception as error:
        print(f"Error: {error}", file=sys.stderr)
        raise SystemExit(1)
