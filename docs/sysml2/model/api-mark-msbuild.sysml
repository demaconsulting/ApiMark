package ApiMarkMsbuild {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * ApiMarkMsbuild is a single-unit system containing the ApiMarkTask unit; it wraps ApiMarkTool as an MSBuild task.
     */

    part def ApiMarkMsbuildSystem {
        doc /* The ApiMarkMsbuild system as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/ApiMark.MSBuild.PackageTests/PackageIntegrationTests.cs");
            designFiles = ("docs/design/api-mark-msbuild.md");
            verificationFiles = ("docs/verification/api-mark-msbuild.md");
            requirementFiles = ("docs/reqstream/api-mark-msbuild.yaml");
        }

        part apiMarkTask : ApiMarkTask;
    }
}
