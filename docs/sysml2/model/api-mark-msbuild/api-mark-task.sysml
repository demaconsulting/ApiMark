package ApiMarkMsbuild {
    private import SoftwareItemMetadata::*;

    part def ApiMarkTask {
        doc /* ApiMarkTask is the MSBuild task entry point for ApiMarkMsbuild; it fires ApiMarkTool out-of-process and reports results back to MSBuild. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.MSBuild/ApiMarkTask.cs");
            testFiles = ("test/ApiMark.MSBuild.Tests/ApiMarkTaskTests.cs");
            designFiles = ("docs/design/api-mark-msbuild/api-mark-task.md");
            verificationFiles = ("docs/verification/api-mark-msbuild/api-mark-task.md");
            requirementFiles = ("docs/reqstream/api-mark-msbuild/api-mark-task.yaml");
        }
    }
}
