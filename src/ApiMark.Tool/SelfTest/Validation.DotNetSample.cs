// cspell:ignore Ldfld Newobj
using System.Reflection;
using System.Reflection.Emit;

namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides the sample .NET assembly and XML documentation used by the .NET self-tests.
/// </summary>
internal static partial class Validation
{
    /// <summary>The name of the sample assembly and of its only namespace.</summary>
    private const string SampleAssemblyName = "ApiMarkSample";

    /// <summary>The XML documentation ID of the sample's <c>Thermostat.Read</c> method.</summary>
    private const string SampleReadDocumentationId = "M:ApiMarkSample.Thermostat.Read";

    /// <summary>The XML documentation entries of the sample assembly.</summary>
    private static readonly (string Id, string Content)[] SampleDocumentation =
    [
        ("T:ApiMarkSample.Reading", "<summary>A single temperature reading.</summary>"),
        ("M:ApiMarkSample.Reading.#ctor(System.Double)",
            "<summary>Creates a reading.</summary><param name=\"celsius\">The measured temperature in degrees Celsius.</param>"),
        ("P:ApiMarkSample.Reading.Celsius", "<summary>Gets the measured temperature in degrees Celsius.</summary>"),
        ("T:ApiMarkSample.Thermostat", "<summary>Regulates a heater to hold a target temperature.</summary>"),
        ("M:ApiMarkSample.Thermostat.#ctor(System.Double)",
            "<summary>Creates a thermostat.</summary><param name=\"initialTarget\">The initial target temperature in degrees Celsius.</param>"),
        ("M:ApiMarkSample.Thermostat.SetTarget(System.Double)",
            "<summary>Sets the target temperature.</summary><param name=\"celsius\">The new target temperature in degrees Celsius.</param>"),
        ("M:ApiMarkSample.Thermostat.SetTarget(System.Double,System.Double)",
            "<summary>Sets the target temperature and the allowed deviation.</summary>" +
            "<param name=\"celsius\">The new target temperature in degrees Celsius.</param>" +
            "<param name=\"tolerance\">The allowed deviation in degrees Celsius.</param>"),
        (SampleReadDocumentationId,
            "<summary>Takes a reading from the sensor.</summary><returns>The current <see cref=\"T:ApiMarkSample.Reading\"/>.</returns>" +
            "<remarks>The target is set with <see cref=\"M:ApiMarkSample.Thermostat.SetTarget(System.Double,System.Double)\"/>.</remarks>"),
    ];

    /// <summary>
    ///     Writes the sample assembly <c>ApiMarkSample.dll</c> and its XML documentation file
    ///     <c>ApiMarkSample.xml</c> into a directory.
    /// </summary>
    /// <remarks>
    ///     The assembly is built in-process with <see cref="PersistedAssemblyBuilder"/>, so the
    ///     .NET self-tests need no compiler, SDK, or fixture file. It declares two public sealed
    ///     classes in the <c>ApiMarkSample</c> namespace:
    ///     <c>Reading</c> (constructor <c>Reading(double celsius)</c> and property <c>double Celsius</c>) and
    ///     <c>Thermostat</c> (constructor <c>Thermostat(double initialTarget)</c>, the overloads
    ///     <c>void SetTarget(double celsius)</c> and <c>void SetTarget(double celsius, double tolerance)</c>,
    ///     and method <c>Reading Read()</c>), giving eight documentable public items, one cross-type
    ///     reference, and one overload page whose sections are linked by heading anchor.
    /// </remarks>
    /// <param name="directory">The directory to write into.</param>
    /// <param name="documentRead">
    ///     <see langword="false"/> to leave the <c>Thermostat.Read</c> method undocumented in the
    ///     XML documentation file.
    /// </param>
    /// <returns>The paths of the written assembly and XML documentation file.</returns>
    private static (string AssemblyPath, string XmlDocPath) WriteSampleAssembly(string directory, bool documentRead = true)
    {
        var assemblyBuilder = new PersistedAssemblyBuilder(new AssemblyName(SampleAssemblyName), typeof(object).Assembly);
        var module = assemblyBuilder.DefineDynamicModule(SampleAssemblyName);
        var objectConstructor = typeof(object).GetConstructor(Type.EmptyTypes)!;
        const MethodAttributes ConstructorAttributes =
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;

        // Reading: an immutable temperature value
        var reading = module.DefineType(
            $"{SampleAssemblyName}.Reading",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);
        var celsiusField = reading.DefineField("_celsius", typeof(double), FieldAttributes.Private | FieldAttributes.InitOnly);
        var readingConstructor = reading.DefineConstructor(ConstructorAttributes, CallingConventions.Standard, [typeof(double)]);
        readingConstructor.DefineParameter(1, ParameterAttributes.None, "celsius");
        EmitStoreArgument(readingConstructor.GetILGenerator(), objectConstructor, celsiusField);
        var celsiusGetter = reading.DefineMethod(
            "get_Celsius",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            typeof(double),
            Type.EmptyTypes);
        var il = celsiusGetter.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, celsiusField);
        il.Emit(OpCodes.Ret);
        reading.DefineProperty("Celsius", PropertyAttributes.None, typeof(double), null).SetGetMethod(celsiusGetter);
        reading.CreateType();

        // Thermostat: holds a target temperature and returns readings
        var thermostat = module.DefineType(
            $"{SampleAssemblyName}.Thermostat",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);
        var targetField = thermostat.DefineField("_target", typeof(double), FieldAttributes.Private);
        var thermostatConstructor = thermostat.DefineConstructor(ConstructorAttributes, CallingConventions.Standard, [typeof(double)]);
        thermostatConstructor.DefineParameter(1, ParameterAttributes.None, "initialTarget");
        EmitStoreArgument(thermostatConstructor.GetILGenerator(), objectConstructor, targetField);
        var setTarget = thermostat.DefineMethod("SetTarget", MethodAttributes.Public | MethodAttributes.HideBySig, typeof(void), [typeof(double)]);
        setTarget.DefineParameter(1, ParameterAttributes.None, "celsius");
        EmitStoreArgument(setTarget.GetILGenerator(), null, targetField);
        var setTargetWithTolerance = thermostat.DefineMethod(
            "SetTarget",
            MethodAttributes.Public | MethodAttributes.HideBySig,
            typeof(void),
            [typeof(double), typeof(double)]);
        setTargetWithTolerance.DefineParameter(1, ParameterAttributes.None, "celsius");
        setTargetWithTolerance.DefineParameter(2, ParameterAttributes.None, "tolerance");
        EmitStoreArgument(setTargetWithTolerance.GetILGenerator(), null, targetField);
        var read = thermostat.DefineMethod("Read", MethodAttributes.Public | MethodAttributes.HideBySig, reading, Type.EmptyTypes);
        il = read.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, targetField);
        il.Emit(OpCodes.Newobj, readingConstructor);
        il.Emit(OpCodes.Ret);
        thermostat.CreateType();

        var assemblyPath = Path.Join(directory, $"{SampleAssemblyName}.dll");
        assemblyBuilder.Save(assemblyPath);

        var xmlDocPath = Path.Join(directory, $"{SampleAssemblyName}.xml");
        var members = SampleDocumentation
            .Where(entry => documentRead || entry.Id != SampleReadDocumentationId)
            .Select(entry => $"    <member name=\"{entry.Id}\">{entry.Content}</member>\n");
        File.WriteAllText(
            xmlDocPath,
            $"<?xml version=\"1.0\"?>\n<doc>\n  <assembly><name>{SampleAssemblyName}</name></assembly>\n  <members>\n" +
            string.Concat(members) +
            "  </members>\n</doc>\n");
        return (assemblyPath, xmlDocPath);
    }

    /// <summary>
    ///     Emits the body <c>{ [base();] this.field = arg1; }</c> of a constructor or setter method.
    /// </summary>
    /// <param name="il">The IL generator of the method.</param>
    /// <param name="baseConstructor">The base constructor to call first, or <see langword="null"/> for none.</param>
    /// <param name="field">The field that receives the first argument.</param>
    private static void EmitStoreArgument(ILGenerator il, ConstructorInfo? baseConstructor, FieldInfo field)
    {
        if (baseConstructor != null)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseConstructor);
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);
    }
}
