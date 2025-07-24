using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using Spectre.Console;

namespace BuildScripts;

[TaskName("Build macOS")]
[IsDependentOn(typeof(PrepTask))]
[IsDependeeOf(typeof(BuildLibraryTask))]
public sealed class BuildMacOSTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) => context.IsRunningOnMacOs();

    public override void Run(BuildContext context)
    {
        var buildWorkingDir = "openal-soft/build_osx";
        context.CreateDirectory(buildWorkingDir);
        context.CreateDirectory($"{context.ArtifactsDir}/osx/");
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "-DCMAKE_OSX_ARCHITECTURES=\"x86_64;arm64\" -DALSOFT_REQUIRE_COREAUDIO=ON -DALSOFT_TESTS=OFF -DALSOFT_UTILS=OFF -DALSOFT_EXAMPLES=OFF -DALSOFT_INSTALL=OFF -DCMAKE_BUILD_TYPE=Release .." });
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "--build . --config Release" });
        var files = Directory.GetFiles(System.IO.Path.Combine(buildWorkingDir), "libopenal.*.*.*.dylib", SearchOption.TopDirectoryOnly);
        context.CopyFile(files[0], $"{context.ArtifactsDir}/osx/libopenal.dylib");
        // Build iOS device and simulator binaries as frameworks instead of dylibs
        BuildiOS(context, "arm64", "ios-arm64", false, "Release-iphoneos");
        BuildiOS(context, "x86_64", "iossimulator-x64", true, "Release-iphonesimulator");
        BuildiOS(context, "arm64", "iossimulator-arm64", true, "Release-iphonesimulator");
        
        // Create universal iOS XCFramework combining all architectures
        CreateiOSXCFramework(context);
    }

    void BuildiOS (BuildContext context, string arch, string rid, bool simulator = false, string releaseDir = "")
    {
        var buildWorkingDir = $"openal-soft/build_{rid}";
        context.CreateDirectory(buildWorkingDir);
        context.CreateDirectory($"{context.ArtifactsDir}/{rid}/");
        var sdk = "";
        if (simulator) {
            IEnumerable<string> output;
            context.StartProcess("xcodebuild", new ProcessSettings { WorkingDirectory = buildWorkingDir, RedirectStandardOutput = true, Arguments="-version -sdk iphonesimulator Path"}, out output);
            //This does not work when used as an argument? $(xcodebuild -version -sdk iphonesimulator Path)";
            sdk = $" -DCMAKE_OSX_SYSROOT={output.First()}";
        }
        // Build static library instead of dylib for iOS
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = $"-GXcode -DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_ARCHITECTURES=\"{arch}\" -DALSOFT_REQUIRE_COREAUDIO=ON -DALSOFT_TESTS=OFF -DALSOFT_UTILS=OFF -DALSOFT_EXAMPLES=OFF -DALSOFT_INSTALL=OFF -DCMAKE_BUILD_TYPE=Release -DLIBTYPE=STATIC{sdk} .." });
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = $"--build . --config Release" });
        
        // Create iOS Framework structure
        var frameworkName = "OpenAL";
        var frameworkDir = $"{context.ArtifactsDir}/{rid}/{frameworkName}.framework";
        context.CreateDirectory(frameworkDir);
        context.CreateDirectory($"{frameworkDir}/Headers");
        
        // Find and copy the static library
        var staticLibFiles = Directory.GetFiles(System.IO.Path.Combine(buildWorkingDir, releaseDir), "libopenal.a", SearchOption.TopDirectoryOnly);
        if (staticLibFiles.Length > 0)
        {
            context.CopyFile(staticLibFiles[0], $"{frameworkDir}/{frameworkName}");
        }
        
        // Copy headers
        var headerSourceDir = "openal-soft/include/AL";
        var headerFiles = Directory.GetFiles(headerSourceDir, "*.h", SearchOption.TopDirectoryOnly);
        foreach (var headerFile in headerFiles)
        {
            var fileName = System.IO.Path.GetFileName(headerFile);
            context.CopyFile(headerFile, $"{frameworkDir}/Headers/{fileName}");
        }
        
        // Create Info.plist for the framework
        CreateFrameworkInfoPlist(context, frameworkDir, frameworkName, simulator);
    }

    void CreateFrameworkInfoPlist(BuildContext context, string frameworkDir, string frameworkName, bool isSimulator)
    {
        // Determine the correct supported platforms based on whether this is a simulator build
        var supportedPlatforms = isSimulator ? "        <string>iPhoneSimulator</string>" : "        <string>iPhoneOS</string>";
        
        var infoPlistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>English</string>
    <key>CFBundleExecutable</key>
    <string>{frameworkName}</string>
    <key>CFBundleIdentifier</key>
    <string>net.monogame.{frameworkName.ToLower()}</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>{frameworkName}</string>
    <key>CFBundlePackageType</key>
    <string>FMWK</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>CFBundleSupportedPlatforms</key>
    <array>
{supportedPlatforms}
    </array>
    <key>MinimumOSVersion</key>
    <string>9.0</string>
</dict>
</plist>";
        
        File.WriteAllText(System.IO.Path.Combine(frameworkDir, "Info.plist"), infoPlistContent);
    }

    string? CreateCombinedSimulatorFramework(BuildContext context, string frameworkName)
    {
        var x64SimulatorFramework = $"{context.ArtifactsDir}/iossimulator-x64/{frameworkName}.framework";
        var arm64SimulatorFramework = $"{context.ArtifactsDir}/iossimulator-arm64/{frameworkName}.framework";
        var combinedSimulatorFramework = $"{context.ArtifactsDir}/iossimulator-combined/{frameworkName}.framework";
        
        // Check if both simulator frameworks exist
        if (!Directory.Exists(x64SimulatorFramework) || !File.Exists($"{x64SimulatorFramework}/{frameworkName}"))
        {
            AnsiConsole.MarkupLine($"[yellow]x86_64 simulator framework not found at {x64SimulatorFramework}[/]");
            return null;
        }
        
        if (!Directory.Exists(arm64SimulatorFramework) || !File.Exists($"{arm64SimulatorFramework}/{frameworkName}"))
        {
            AnsiConsole.MarkupLine($"[yellow]arm64 simulator framework not found at {arm64SimulatorFramework}[/]");
            return null;
        }
        
        // Create combined simulator framework directory
        context.CreateDirectory($"{context.ArtifactsDir}/iossimulator-combined/");
        
        if (Directory.Exists(combinedSimulatorFramework))
        {
            Directory.Delete(combinedSimulatorFramework, true);
        }
        
        context.CreateDirectory(combinedSimulatorFramework);
        context.CreateDirectory($"{combinedSimulatorFramework}/Headers");
        
        // Use lipo to combine the x86_64 and arm64 simulator binaries
        var x64Binary = $"{x64SimulatorFramework}/{frameworkName}";
        var arm64Binary = $"{arm64SimulatorFramework}/{frameworkName}";
        var combinedBinary = $"{combinedSimulatorFramework}/{frameworkName}";
        
        AnsiConsole.MarkupLine($"[blue]Combining simulator binaries using lipo...[/]");
        var lipoArgs = $"-create \"{x64Binary}\" \"{arm64Binary}\" -output \"{combinedBinary}\"";
        var result = context.StartProcess("lipo", new ProcessSettings { Arguments = lipoArgs });
        
        if (result != 0)
        {
            AnsiConsole.MarkupLine($"[red]Failed to combine simulator binaries with lipo. Exit code: {result}[/]");
            return null;
        }
        
        // Copy headers from one of the simulator frameworks (they should be identical)
        var headerSourceDir = $"{x64SimulatorFramework}/Headers";
        var headerFiles = Directory.GetFiles(headerSourceDir, "*.h", SearchOption.TopDirectoryOnly);
        foreach (var headerFile in headerFiles)
        {
            var fileName = System.IO.Path.GetFileName(headerFile);
            context.CopyFile(headerFile, $"{combinedSimulatorFramework}/Headers/{fileName}");
        }
        
        // Create Info.plist for the combined simulator framework
        CreateFrameworkInfoPlist(context, combinedSimulatorFramework, frameworkName, true);
        
        AnsiConsole.MarkupLine($"[green]Successfully created combined simulator framework at: {combinedSimulatorFramework}[/]");
        return combinedSimulatorFramework;
    }

    void CreateiOSXCFramework(BuildContext context)
    {
        var frameworkName = "OpenAL";
        var xcframeworkPath = $"{context.ArtifactsDir}/ios/{frameworkName}.xcframework";
        
        // Remove any existing XCFramework
        if (Directory.Exists(xcframeworkPath))
        {
            Directory.Delete(xcframeworkPath, true);
        }
        
        // Create combined simulator framework by merging x86_64 and arm64 simulator binaries using lipo
        var combinedSimulatorFrameworkPath = CreateCombinedSimulatorFramework(context, frameworkName);
        
        // Collect framework paths - device and combined simulator
        var frameworkPaths = new List<string>();
        
        // Add device framework
        var deviceFrameworkDir = $"{context.ArtifactsDir}/ios-arm64/{frameworkName}.framework";
        if (Directory.Exists(deviceFrameworkDir) && File.Exists($"{deviceFrameworkDir}/{frameworkName}"))
        {
            frameworkPaths.Add(deviceFrameworkDir);
            AnsiConsole.MarkupLine($"[green]Found device framework at {deviceFrameworkDir}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Device framework not found at {deviceFrameworkDir}[/]");
        }
        
        // Add combined simulator framework
        if (!string.IsNullOrEmpty(combinedSimulatorFrameworkPath))
        {
            frameworkPaths.Add(combinedSimulatorFrameworkPath);
            AnsiConsole.MarkupLine($"[green]Found combined simulator framework at {combinedSimulatorFrameworkPath}[/]");
        }
        
        if (frameworkPaths.Count > 0)
        {
            // Build xcodebuild command to create XCFramework
            var xcframeworkArgs = new List<string> { "-create-xcframework" };
            
            foreach (var frameworkPath in frameworkPaths)
            {
                xcframeworkArgs.Add("-framework");
                xcframeworkArgs.Add(frameworkPath);
            }
            
            xcframeworkArgs.Add("-output");
            xcframeworkArgs.Add(xcframeworkPath);
            
            var arguments = string.Join(" ", xcframeworkArgs.Select(arg => arg.Contains(" ") ? $"\"{arg}\"" : arg));
            
            AnsiConsole.MarkupLine($"[blue]Creating XCFramework with command: xcodebuild {arguments}[/]");
            
            var result = context.StartProcess("xcodebuild", new ProcessSettings { Arguments = arguments });
            
            if (result == 0)
            {
                AnsiConsole.MarkupLine($"[green]Successfully created XCFramework at: {xcframeworkPath}[/]");
                AnsiConsole.MarkupLine($"[green]XCFramework contains {frameworkPaths.Count} platform-specific frameworks[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Failed to create XCFramework. Exit code: {result}[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]No framework paths found for XCFramework creation[/]");
        }
    }



    void BuildAndroid (BuildContext context, string arch, string rid, string minNdk)
    {
        var ndk = System.Environment.GetEnvironmentVariable ("ANDROID_NDK_HOME");
        var buildWorkingDir = $"openal-soft/build_android_{arch}";
        System.IO.Directory.CreateDirectory(buildWorkingDir);
        System.IO.Directory.CreateDirectory($"{context.ArtifactsDir}/{rid}");
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = $"-DANDROID_ABI={arch} -DANDROID_PLATFORM={minNdk} -DCMAKE_TOOLCHAIN_FILE={ndk}/build/cmake/android.toolchain.cmake -DALSOFT_EMBED_HRTF_DATA=TRUE -DALSOFT_REQUIRE_OPENSL=ON -DCMAKE_BUILD_TYPE=Release -DANDROID_NDK={ndk} .." });
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "--build . --config Release" });
        context.CopyFile($"{buildWorkingDir}/libopenal.so", $"{context.ArtifactsDir}/{rid}/libopenal.so");
    }
}
