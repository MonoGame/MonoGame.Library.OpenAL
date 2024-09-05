using Cake.Common.Tools.MSBuild;

namespace BuildScripts;

[TaskName("Build Windows")]
[IsDependentOn(typeof(PrepTask))]
[IsDependeeOf(typeof(BuildLibraryTask))]
public sealed class BuildWindowsTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) => context.IsRunningOnWindows();

    public override void Run(BuildContext context)
    {
        var buildWorkingDir = "openal-soft/build_windows_x64";
        context.CreateDirectory(buildWorkingDir);
        context.CreateDirectory($"{context.ArtifactsDir}/win-x64/");
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "-DALSOFT_TESTS=OFF -DALSOFT_UTILS=OFF -DALSOFT_EXAMPLES=OFF -DALSOFT_INSTALL=OFF -D CMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded -A x64 .." });
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "--build . --config release" });
        context.CopyFile(@"openal-soft/build_windows_x64/Release/OpenAL32.dll", $"{context.ArtifactsDir}/win-x64/openal.dll");

        buildWorkingDir = "openal-soft/build_windows_arm64";
        context.CreateDirectory(buildWorkingDir);
        context.CreateDirectory($"{context.ArtifactsDir}/win-arm64/");
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "-DALSOFT_TESTS=OFF -DALSOFT_UTILS=OFF -DALSOFT_EXAMPLES=OFF -DALSOFT_INSTALL=OFF -D CMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded -A ARM64 .." });
        context.StartProcess("cmake", new ProcessSettings { WorkingDirectory = buildWorkingDir, Arguments = "--build . --config release" });
        context.CopyFile(@"openal-soft/build_windows_arm64/Release/OpenAL32.dll", $"{context.ArtifactsDir}/win-arm64/openal.dll");
    }
}
