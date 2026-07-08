package ApiMarkMsbuild {
    doc
    /*
     * ApiMarkMsbuild is a single-unit system containing the ApiMarkTask unit; it wraps ApiMarkTool as an MSBuild task.
     */

    part def ApiMarkMsbuildSystem {
        doc /* The ApiMarkMsbuild system as a whole. */

        comment testRef /* Test: test/ApiMark.MSBuild.PackageTests/PackageIntegrationTests.cs */
        comment designRef /* Design: docs/design/api-mark-msbuild.md */
        comment verificationRef /* Verification: docs/verification/api-mark-msbuild.md */
        comment reqRef /* Requirements: docs/reqstream/api-mark-msbuild.yaml */

        part apiMarkTask : ApiMarkTask;
    }
}
