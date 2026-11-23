// Views rendered into docs/design/generated/ for the Design document.
// Each system package is a facade, so every 'expose' statement can reference its target part
// def directly through an import. Per SysML2Tools syntax, 'expose' is only valid inside a
// named 'view' usage (not a 'view def' definition), and it - not 'render' - is what scopes the
// rendered diagram content. ApiMark has six independent system packages (unlike a single-system
// repository), so SoftwareStructureView is typed by GeneralView - which draws one package
// frame per system - and exposes each system package's members, filtering out documentation
// so the overview stays readable.
package ApiMarkDesignViews {
    private import StandardViewDefinitions::*;
    private import ApiMarkCore::*;
    private import ApiMarkDotNet::*;
    private import ApiMarkCpp::*;
    private import ApiMarkVhdl::*;
    private import ApiMarkMsbuild::*;
    private import ApiMarkTool::*;

    view SoftwareStructureView : GeneralView {
        expose ApiMarkCore::**;
        expose ApiMarkDotNet::**;
        expose ApiMarkCpp::**;
        expose ApiMarkVhdl::**;
        expose ApiMarkMsbuild::**;
        expose ApiMarkTool::**;
        filter not (@SysML::Documentation);
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