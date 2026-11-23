// Views rendered into docs/design/generated/ for the Design document.
// Each system package is a facade, so every 'expose' statement can reference its target part
// def directly through an import. Per SysML2Tools syntax, 'expose' is only valid inside a
// named 'view' usage (not a 'view def' definition), and it - not 'render' - is what scopes the
// rendered diagram content. ApiMark has six independent system packages (unlike a single-system
// repository), so SoftwareStructureView is typed by InterconnectionView and exposes each system part
// def, which renders as a frame with its direct subsystem usages.
package ApiMarkDesignViews {
    private import StandardViewDefinitions::*;
    private import ApiMarkCore::*;
    private import ApiMarkDotNet::*;
    private import ApiMarkCpp::*;
    private import ApiMarkVhdl::*;
    private import ApiMarkMsbuild::*;
    private import ApiMarkTool::*;

    view SoftwareStructureView : InterconnectionView {
        expose ApiMarkCore::ApiMarkCoreSystem;
        expose ApiMarkDotNet::ApiMarkDotNetSystem;
        expose ApiMarkCpp::ApiMarkCppSystem;
        expose ApiMarkVhdl::ApiMarkVhdlSystem;
        expose ApiMarkMsbuild::ApiMarkMsbuildSystem;
        expose ApiMarkTool::ApiMarkToolSystem;
    }

    view ApiMarkCoreView {
        expose ApiMarkCoreSystem;
    }

    view ApiMarkDotNetView {
        expose ApiMarkDotNetSystem;
    }

    view ApiMarkCppView {
        expose ApiMarkCppSystem;
    }

    view ApiMarkVhdlView {
        expose ApiMarkVhdlSystem;
    }

    view ApiMarkMsbuildView {
        expose ApiMarkMsbuildSystem;
    }

    view ApiMarkToolView {
        expose ApiMarkToolSystem;
    }

    view CliView {
        expose CliSubsystem;
    }

    view SelfTestView {
        expose SelfTestSubsystem;
    }
}