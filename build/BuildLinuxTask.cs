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
        context.CreateDirectory(buildWorkingDir);
        context.CreateDirectory($"{context.ArtifactsDir}/linux-x64/");
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "-DALSOFT_TESTS=OFF -DALSOFT_UTILS=OFF -DALSOFT_EXAMPLES=OFF -DALSOFT_INSTALL=OFF -DALSOFT_BACKEND_SNDIO=OFF -DCMAKE_BUILD_TYPE=Release .." });
        context.StartProcess("make", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "" });
        context.CopyFile($"{buildWorkingDir}/libopenal.so", $"{context.ArtifactsDir}/linux-x64/libopenal.so");
        BuildAndroid (context, "arm64-v8a", "android-arm64", "23");
        BuildAndroid (context, "armeabi-v7a", "android-arm", "23");
        BuildAndroid (context, "x86", "android-x86", "23");
        BuildAndroid (context, "x86_64", "android-x64", "23");
    }

    void BuildAndroid (BuildContext context, string arch, string rid, string minNdk)
    {
        var ndk = System.Environment.GetEnvironmentVariable ("ANDROID_NDK_HOME");
        var buildWorkingDir = $"openal-soft/build_android_{arch}";
        context.CreateDirectory(buildWorkingDir);
        context.CreateDirectory($"{context.ArtifactsDir}/{rid}");
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = $"-DANDROID_ABI={arch} -DANDROID_PLATFORM={minNdk} -DCMAKE_TOOLCHAIN_FILE={ndk}/build/cmake/android.toolchain.cmake -DALSOFT_EMBED_HRTF_DATA=TRUE -DALSOFT_REQUIRE_OPENSL=ON -DCMAKE_BUILD_TYPE=Release -DANDROID_NDK={ndk} .." });
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "--build . --config Release" });
        context.CopyFile($"{buildWorkingDir}/libopenal.so", $"{context.ArtifactsDir}/{rid}/libopenal.so");
    }
}
