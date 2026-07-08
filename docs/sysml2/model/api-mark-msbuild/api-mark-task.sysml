package ApiMarkMsbuild {
    part def ApiMarkTask {
        doc /* ApiMarkTask is the MSBuild task entry point for ApiMarkMsbuild; it fires ApiMarkTool out-of-process and reports results back to MSBuild. */

        comment sourceRef /* Source: src/ApiMark.MSBuild/ApiMarkTask.cs */
        comment testRef /* Test: test/ApiMark.MSBuild.Tests/ApiMarkTaskTests.cs */
        comment designRef /* Design: docs/design/api-mark-msbuild/api-mark-task.md */
        comment verificationRef /* Verification: docs/verification/api-mark-msbuild/api-mark-task.md */
        comment reqRef /* Requirements: docs/reqstream/api-mark-msbuild/api-mark-task.yaml */
    }
}
