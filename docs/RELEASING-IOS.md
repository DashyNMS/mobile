# Releasing to TestFlight

The **TestFlight** workflow (`.github/workflows/testflight.yml`) builds a signed
App Store IPA on a GitHub macOS runner and uploads it to App Store Connect.
You don't need a Mac. The setup below is done once; after that, each release is
one click.

## One-time setup

### 1. The bundle ID

The bundle ID is `net.pckp.DashyNMS` (`ApplicationId` in
`src/DashyNMS.Mobile/DashyNMS.Mobile.csproj`). The same value is the Android
package name. Use exactly this, same case, in the App ID and App Store
Connect record below. Once a build has been uploaded, the ID can't change
without starting a new app.

### 2. Register the App ID

[developer.apple.com](https://developer.apple.com/account) → Certificates,
Identifiers & Profiles → **Identifiers** → **+** → App IDs → App.
Use an explicit bundle ID matching the one above. No extra capabilities are
needed yet.

### 3. Create an Apple Distribution certificate

You need a certificate signing request (CSR). If you use a Mac, create it
with Keychain Access → Certificate Assistant → *Request a Certificate From a
Certificate Authority* (saved to disk). Without a Mac, use `openssl`:

```sh
openssl genrsa -out dist.key 2048
openssl req -new -key dist.key -out dist.csr -subj "/emailAddress=you@example.com/CN=DashyNMS Distribution/C=GB"
```

Then: **Certificates** → **+** → *Apple Distribution* → upload the CSR →
download `distribution.cer`.

Package the certificate and its private key as a password-protected `.p12`:

```sh
openssl x509 -inform DER -in distribution.cer -out dist.pem
openssl pkcs12 -export -legacy -inkey dist.key -in dist.pem -out dist.p12   # asks for a password
```

(If you made the CSR on a Mac, export the certificate from Keychain Access as
`.p12` instead.) Keep `dist.key` / `dist.p12` somewhere safe and never commit them.

### 4. Create an App Store provisioning profile

**Profiles** → **+** → *App Store Connect* (under Distribution) → pick the
App ID from step 2 → pick the certificate from step 3 → give it a name →
download the `.mobileprovision`.

### 5. Create the app in App Store Connect

[appstoreconnect.apple.com](https://appstoreconnect.apple.com) → **Apps** →
**+** → New App → iOS, name *DashyNMS* (or whatever's free), the bundle ID
from step 2, and any SKU (for example `dashynms-mobile`).

### 6. Create an App Store Connect API key

App Store Connect → **Users and Access** → **Integrations** → *App Store
Connect API* → Team Keys → **+**. Name it (for example *GitHub TestFlight*)
and give it the **App Manager** role. Note the **Key ID** and the **Issuer
ID** shown above the list, and download the `.p8`. Apple only lets you
download it once.

### 7. Add the GitHub secrets

In the repo: Settings → Secrets and variables → Actions → *New repository secret*:

| Secret | Value |
| --- | --- |
| `IOS_DIST_CERT_P12_BASE64` | `base64 -i dist.p12` (on Linux: `base64 -w0 dist.p12`) |
| `IOS_DIST_CERT_PASSWORD` | the `.p12` password |
| `IOS_PROVISIONING_PROFILE_BASE64` | `base64 -i YourProfile.mobileprovision` |
| `APPSTORE_API_KEY_ID` | Key ID from step 6 |
| `APPSTORE_API_ISSUER_ID` | Issuer ID from step 6 |
| `APPSTORE_API_PRIVATE_KEY` | the whole contents of the `.p8`, including the `BEGIN`/`END` lines |

## Each release

1. Bump `<Version>` in `Directory.Build.props` if this is a new version
   (the display version, for example `1.1.0`). The build number is set
   automatically from the workflow run number, so repeat uploads of the same
   version are fine.
2. Actions → **TestFlight** → *Run workflow* → choose the branch.
3. When the job finishes, the build shows in App Store Connect → your app →
   **TestFlight** after Apple's processing (typically 5–30 minutes).
4. **Internal testing** (up to 100 members of your App Store Connect team):
   create an internal group, add the build, and testers get it in the
   TestFlight app straight away.
   **External testing** (anyone with an email address or a public link): add
   the build to an external group. The first build of each version goes
   through a short Beta App Review. Apple will want a test account or notes
   explaining that the app needs the tester's own LibreNMS server.

## Notes

- `ITSAppUsesNonExemptEncryption` is `false` in `Platforms/iOS/Info.plist`,
  because the app only uses standard HTTPS. This skips the export-compliance
  question on every build. Revisit it if the app ever adds its own cryptography.
- The distribution certificate expires after a year, and the profile expires
  with it. Renew both and update the three signing secrets.
- To build locally on a Mac instead, install the certificate and profile,
  then run the same `dotnet publish` command as the workflow (note it passes both `-p:MobilePlatform=ios` and `-f net10.0-ios`) and upload the
  IPA with Apple's **Transporter** app.
