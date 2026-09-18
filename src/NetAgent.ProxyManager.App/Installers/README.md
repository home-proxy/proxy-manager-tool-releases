# Bundled Proxifier Installer

Copy the official `ProxifierSetup.exe` into this folder before publishing a release:

```txt
src/NetAgent.ProxyManager.App/Installers/ProxifierSetup.exe
```

Then update `Proxifier:InstallerSha256` in `src/NetAgent.ProxyManager.App/appsettings.json` with the SHA-256 of that file. The app will refuse to run the bundled installer when the hash is missing or does not match.

## How it ships now (embedded)

When this file is present at build time it is **embedded into `ProxyManager.App.exe`**
(`<EmbeddedResource ... LogicalName="Installers/ProxifierSetup.exe" />` in the csproj).
At startup the app extracts it once to `%AppData%\ProxyManager\Installers\ProxifierSetup.exe`
and, if Proxifier is not already installed, installs it silently on first run
(`MainForm.TryAutoInstallProxifierSilentlyAsync` → `ProxifierInstallerService`, elevated,
SHA-256 verified). This means the standalone single-file exe can self-install Proxifier
without shipping any loose file next to it.

- The `Condition="Exists(...)"` on the embed keeps CI builds working before this binary is
  committed; in that case the exe simply has no Proxifier installer and the auto-install
  step is skipped silently (users can still install from Settings).
- Because it is a licensed binary, decide deliberately whether to commit it to the repo or
  inject it in CI. The SHA-256 in `appsettings.json` must match the exact file you use.
