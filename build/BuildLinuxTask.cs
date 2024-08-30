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
        var buildWorkingDir = "openal-soft/";
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "-DALSOFT_TESTS=OFF -DALSOFT_UTILS=OFF -DALSOFT_EXAMPLES=OFF -DALSOFT_INSTALL=OFF CMakeLists.txt" });
        context.StartProcess("make", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "" });
        context.CopyFile($"{buildWorkingDir}libopenal.so", $"{context.ArtifactsDir}/libopenal.so");
    }

    void BuildAndroid (BuildContext context, string arch, string minNdk)
    {
        //cmake -DANDROID_ABI={arch} -DANDROID_PLATFORM={minNdk} -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake -DALSOFT_EMBED_HRTF_DATA=TRUE -DALSOFT_REQUIRE_OPENSL=ON -DCMAKE_BUILD_TYPE=Release -DANDROID_NDK=$ANDROID_NDK_HOME ..
        var buildWorkingDir = $"openal-soft/build_android_{arch}";
        System.IO.Directory.CreateDirectory(buildWorkingDir);
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "-DANDROID_ABI={arch} -DANDROID_PLATFORM={minNdk} -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake -DALSOFT_EMBED_HRTF_DATA=TRUE -DALSOFT_REQUIRE_OPENSL=ON -DCMAKE_BUILD_TYPE=Release -DANDROID_NDK=$ANDROID_NDK_HOME .." });
        context.StartProcess("make", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "" });
        context.CopyFile($"{buildWorkingDir}libopenal.so", $"{context.ArtifactsDir}/libopenal.so");
    }
}
