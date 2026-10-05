# HeadsetControl packaging input

This directory is intentionally incomplete in source control. Before building a release installer, provide the exact Windows x64 HeadsetControl build verified during the M0 hardware test:

```text
packaging/headsetcontrol/
└── headsetcontrol.exe
```

Do not substitute a newer `headsetcontrol.exe` without repeating the JSON compatibility and physical-headset checks described in the implementation plan.

## Obtaining the executable

Use only the [official HeadsetControl releases](https://github.com/Sapd/HeadsetControl/releases) from the `Sapd/HeadsetControl` repository. HeadsetControl's own documentation says Windows users can download either an installer or a portable executable from that page.

1. Choose a tagged stable release that includes the Logitech and HyperX fixes required by this project. Avoid the rolling **Continuous Build** unless a needed fix has not reached a stable release; continuous builds can change without a versioned release process.
2. Expand the release's **Assets** list and download the Windows x86-64 portable executable when available. Its name is normally similar to `headsetcontrol-windows-x86_64.exe`. Rename a copied packaging input to exactly `headsetcontrol.exe`.
3. If that release provides only `headsetcontrol-windows-x86_64-setup.exe`, run the official installer, then locate the installed executable. First try this in PowerShell:

   ```powershell
   (Get-Command headsetcontrol.exe -ErrorAction SilentlyContinue).Source
   ```

   If it is not on `PATH`, search the normal per-user and system application locations:

   ```powershell
   Get-ChildItem `
     $env:LOCALAPPDATA, $env:ProgramFiles, ${env:ProgramFiles(x86)} `
     -Filter headsetcontrol.exe -File -Recurse -ErrorAction SilentlyContinue `
     | Select-Object -ExpandProperty FullName
   ```

4. Verify the release checksum and, where provided, the matching `.asc` GPG signature using the instructions and signing-key fingerprint on the release page. Do not obtain the binary from a third-party download site.
5. Test the exact executable on the target Windows 10 computer before copying it here:

   ```powershell
   .\headsetcontrol.exe -o json
   .\headsetcontrol.exe -b -o json
   ```

   Save redacted results for each headset powered on and off, then both connected together. Confirm the JSON contains the expected `version`, compatible `api_version`, device identities, and plausible battery states.
6. Copy—not move—the verified executable into this directory as `headsetcontrol.exe`.

You can record the executable's hash for your own release notes with:

```powershell
Get-FileHash .\headsetcontrol.exe -Algorithm SHA256
```

The installer does not require separate local licence or source-description files. HeadsetControl is GPL-3.0 software; its licence, source, and releases are available from the upstream repository. Confirm that your chosen distribution method satisfies the licence before distributing the combined installer.
