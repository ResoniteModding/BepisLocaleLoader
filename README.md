# BepisLocaleLoader
A [Resonite](https://resonite.com/) mod that [TODO: describe what your mod does here].

## Installation (Manual)
1. Install [BepisLoader](https://github.com/ResoniteModding/BepisLoader) for Resonite.
2. Download the latest release ZIP file (e.g., `NepuShiro-BepisLocaleLoader-1.0.0.zip`) from the [Releases](https://github.com/NepuShiro/BepisLocaleLoader/releases) page.
3. Extract the ZIP and copy the `plugins` folder to your BepInEx folder in your Resonite installation directory:
   - **Default location:** `C:\Program Files (x86)\Steam\steamapps\common\Resonite\BepInEx\`
4. Start the game. If you want to verify that the mod is working you can check your BepInEx logs.

---

## Template Instructions (TODO: DELETE THIS BEFORE PUBLISHING)

### Getting Started
**Important:** Search for "TODO" throughout your codebase and fill in all the required fields:
- Update the mod description in this README
- Complete any TODO items in your plugin code
- Review and update metadata in the `.csproj` file
- Update the mod details in `thunderstore.toml`

### PluginMetadata
This project uses **BepInEx.ResonitePluginInfoProps** which automatically generates a `PluginMetadata` class from your .csproj properties:
- `PackageID` → `GUID`
- `Product` → `NAME` 
- `Version` → `VERSION`
- `Authors` → `AUTHORS`
- `RepositoryUrl` → `REPOSITORY_URL`

These constants are used in your BepInPlugin attribute, keeping all metadata synchronized in one place.

### Configuration
1. **GamePath** - The project automatically detects your Resonite installation:
   - Checks `ResonitePath` environment variable first
   - Falls back to common Steam installation paths on Windows and Linux
   - If Resonite is not found locally, uses NuGet package `Resonite.GameLibs` for stripped game references
2. **Metadata** - Update your plugin metadata (Version, Authors, Product, etc.) in the `.csproj` file

### Building
```bash
dotnet build              # Debug build
dotnet build -c Release   # Release build
```
This will:
- Compile your plugin using either local game references (if found) or NuGet package references (fallback)
- Copy it to `$(GamePath)/BepInEx/plugins` (if `CopyToPlugins` is true and local game is found)
- Output is placed directly in the project directory (no target framework subdirectory)

### Publishing Your Mod

#### Option 1: Thunderstore
1. Replace the placeholder `icon.png` with your own mod icon ([icon requirements](https://wiki.thunderstore.io/mods/creating-a-package#icon))
2. Update `thunderstore.toml` with your namespace and mod details
3. Build and publish your mod using one of these methods:

##### Manual Upload
```bash
dotnet tcli build  # Creates a ZIP file in ./build folder
```
Then upload the generated ZIP file manually at [Thunderstore Package Creator](https://thunderstore.io/package/create/)

##### CLI Publishing
```bash
# First, get your API token from https://thunderstore.io/settings/teams/
dotnet tcli publish --token tss_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```
This will automatically build and upload your mod to Thunderstore (no need to run `tcli build` first). See [Thunderstore CLI Authentication](https://github.com/thunderstore-io/thunderstore-cli/wiki#authentication) for details on obtaining your API token.

#### Option 2: GitHub Releases (or other platforms)
If you prefer not to use Thunderstore:
1. Build your mod package using one of these methods:
   ```bash
   dotnet tcli build  # Creates a ZIP file in ./build folder
   # OR manually zip the contents of ./dist folder after a Release build
   ```
2. Create a release with version tracking:
   - **Via command line:**
     ```bash
     git tag v1.0.0
     git push origin v1.0.0
     ```
   - **Via website:** Create a new release on your repository's Releases page - this automatically creates a git tag
3. Upload the ZIP file to your GitHub Releases page or preferred distribution platform

### Resources
- [Resonite Modding Documentation](https://modding.resonite.net/) - Comprehensive guide to Resonite modding
- [Thunderstore Wiki](https://wiki.thunderstore.io/) - Complete guide to package creation and publishing
- [Thunderstore CLI](https://github.com/thunderstore-io/thunderstore-cli) - Tool for building and publishing packages