// Views rendered into docs/design/generated/ for the Design document.
// Reopens each system's package so each 'expose' statement can reference its target part
// def directly. Per SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage
// (not a 'view def' definition), and it - not 'render' - is what scopes the rendered
// diagram content. ApiMark has six independent system packages (unlike a single-system
// repository), so SoftwareStructureView exposes each system package individually to produce
// one coherent overview diagram.
package ApiMarkCore {
    view SoftwareStructureView {
        expose ApiMarkCore;
        expose ApiMarkDotNet;
        expose ApiMarkCpp;
        expose ApiMarkVhdl;
        expose ApiMarkMsbuild;
        expose ApiMarkTool;
        render asTreeDiagram;
    }

    view ApiMarkCoreView {
        expose ApiMarkCoreSystem;
        render asTreeDiagram;
    }
}

package ApiMarkDotNet {
    view ApiMarkDotNetView {
        expose ApiMarkDotNetSystem;
        render asTreeDiagram;
    }
}

package ApiMarkCpp {
    view ApiMarkCppView {
        expose ApiMarkCppSystem;
        render asTreeDiagram;
    }
}

package ApiMarkVhdl {
    view ApiMarkVhdlView {
        expose ApiMarkVhdlSystem;
        render asTreeDiagram;
    }
}

package ApiMarkMsbuild {
    view ApiMarkMsbuildView {
        expose ApiMarkMsbuildSystem;
        render asTreeDiagram;
    }
}

package ApiMarkTool {
    view ApiMarkToolView {
        expose ApiMarkToolSystem;
        render asTreeDiagram;
    }

    view CliView {
        expose CliSubsystem;
        render asTreeDiagram;
    }

    view SelfTestView {
        expose SelfTestSubsystem;
        render asTreeDiagram;
    }
}
