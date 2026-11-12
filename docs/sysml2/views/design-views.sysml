// Views rendered into docs/design/generated/ for the Design document.
// Reopens each system's package so each 'expose' statement can reference its target part
// def directly. Per SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage
// (not a 'view def' definition), and it - not 'render' - is what scopes the rendered
// diagram content. ApiMark has six independent system packages (unlike a single-system
// repository), so SoftwareStructureView is typed by GeneralView - which draws one package
// frame per system - and exposes each system package's members, filtering out documentation
// so the overview stays readable.
package ApiMarkCore {
    private import StandardViewDefinitions::*;

    view SoftwareStructureView : GeneralView {
        expose ApiMarkCore::**;
        expose ApiMarkDotNet::**;
        expose ApiMarkCpp::**;
        expose ApiMarkVhdl::**;
        expose ApiMarkMsbuild::**;
        expose ApiMarkTool::**;
        filter not @SysML::Documentation;
    }

    view ApiMarkCoreView {
        expose ApiMarkCoreSystem;
    }
}

package ApiMarkDotNet {
    view ApiMarkDotNetView {
        expose ApiMarkDotNetSystem;
    }
}

package ApiMarkCpp {
    view ApiMarkCppView {
        expose ApiMarkCppSystem;
    }
}

package ApiMarkVhdl {
    view ApiMarkVhdlView {
        expose ApiMarkVhdlSystem;
    }
}

package ApiMarkMsbuild {
    view ApiMarkMsbuildView {
        expose ApiMarkMsbuildSystem;
    }
}

package ApiMarkTool {
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
