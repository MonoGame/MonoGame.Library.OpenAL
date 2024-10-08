using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;

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
        BuildiOS(context, "arm64", "ios-arm64", releaseDir: "Release-iphoneos");
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
            // hard code this for now
            //This does not work when used as an argument? $(xcodebuild -version -sdk iphonesimulator Path)";
            sdk = $" -DCMAKE_OSX_SYSROOT=/Applications/Xcode.app/Contents/Developer/Platforms/iPhoneSimulator.platform/Developer/SDKs/iPhoneSimulator17.5.sdk";
        }
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = $"-GXcode -DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_ARCHITECTURES=\"{arch}\" -DALSOFT_REQUIRE_COREAUDIO=ON -DALSOFT_TESTS=OFF -DALSOFT_UTILS=OFF -DALSOFT_EXAMPLES=OFF -DALSOFT_INSTALL=OFF -DCMAKE_BUILD_TYPE=Release{sdk} .." });
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = $"--build . --config Release" });
        var files = Directory.GetFiles(System.IO.Path.Combine(buildWorkingDir, releaseDir), "libopenal.*.*.*.dylib", SearchOption.TopDirectoryOnly);
        //if (files.Length > 0)
        context.CopyFile(files[0], $"{context.ArtifactsDir}/{rid}/libopenal.dylib");
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
