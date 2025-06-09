using System.Runtime.InteropServices;

namespace BuildScripts;

[TaskName("Build Linux")]
[IsDependentOn(typeof(PrepTask))]
[IsDependeeOf(typeof(BuildLibraryTask))]
public sealed class BuildLinuxTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) => context.IsRunningOnLinux();

    public override void Run(BuildContext context)
    {
        var buildWorkingDir = "openal-soft/build_linux";
        var architecture = RuntimeInformation.ProcessArchitecture;
        var arch = architecture == Architecture.Arm64 ? "arm64" : "x64";
        context.CreateDirectory(buildWorkingDir);
        context.CreateDirectory($"{context.ArtifactsDir}/linux-{arch}/");
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "-DALSOFT_TESTS=OFF -DALSOFT_UTILS=OFF -DALSOFT_EXAMPLES=OFF -DALSOFT_INSTALL=OFF -DALSOFT_BACKEND_SNDIO=OFF -DCMAKE_BUILD_TYPE=Release .." });
        context.StartProcess("make", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "" });
        context.StartProcess ("strip", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = $"--strip-all libopenal.so"});
        context.CopyFile($"{buildWorkingDir}/libopenal.so", $"{context.ArtifactsDir}/linux-{arch}/libopenal.so");
        BuildAndroid (context, "arm64-v8a", "android-arm64", "23");
        BuildAndroid (context, "armeabi-v7a", "android-arm", "23");
        BuildAndroid (context, "x86", "android-x86", "23");
        BuildAndroid (context, "x86_64", "android-x64", "23");
    }

    void BuildAndroid (BuildContext context, string arch, string rid, string minNdk)
    {
        var ndk = System.Environment.GetEnvironmentVariable ("ANDROID_NDK_HOME");
        if (string.IsNullOrEmpty(ndk))
            return;
        var strip = System.IO.Path.Combine (ndk ?? string.Empty, "toolchains", "llvm", "prebuilt", "linux-x86_64","bin","llvm-strip");
        var buildWorkingDir = $"openal-soft/build_android_{arch}";
        context.CreateDirectory(buildWorkingDir);
        context.CreateDirectory($"{context.ArtifactsDir}/{rid}");
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = $"-DANDROID_ABI={arch} -DANDROID_PLATFORM={minNdk} -DCMAKE_TOOLCHAIN_FILE={ndk}/build/cmake/android.toolchain.cmake -DALSOFT_BACKEND_PIPEWIRE=OFF -DALSOFT_TESTS=OFF -DALSOFT_UTILS=OFF -DALSOFT_EXAMPLES=OFF -DALSOFT_INSTALL=OFF -DALSOFT_EMBED_HRTF_DATA=TRUE -DALSOFT_REQUIRE_OPENSL=ON -DCMAKE_BUILD_TYPE=Release -DANDROID_NDK={ndk} .." });
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "--build . --config Release" });
        context.StartProcess (strip, new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = $"--strip-all libopenal.so"});
        context.CopyFile($"{buildWorkingDir}/libopenal.so", $"{context.ArtifactsDir}/{rid}/libopenal.so");
    }
}
