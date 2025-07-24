using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;
using System.IO;
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
        CreateFrameworkInfoPlist(context, frameworkDir, frameworkName, arch);
    }

    void CreateFrameworkInfoPlist(BuildContext context, string frameworkDir, string frameworkName, string arch)
    {
        var infoPlistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>English</string>
    <key>CFBundleExecutable</key>
    <string>{frameworkName}</string>
    <key>CFBundleIdentifier</key>
    <string>org.monogame.{frameworkName.ToLower()}</string>
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
        <string>iPhoneOS</string>
        <string>iPhoneSimulator</string>
    </array>
    <key>MinimumOSVersion</key>
    <string>9.0</string>
</dict>
</plist>";
        
        File.WriteAllText(System.IO.Path.Combine(frameworkDir, "Info.plist"), infoPlistContent);
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
