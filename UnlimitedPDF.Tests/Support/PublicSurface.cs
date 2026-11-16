using System.Reflection;
using System.Runtime.CompilerServices;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Describes an assembly's public surface as text, one line per type and member, in a stable order: everything a
/// caller compiles against, so a change shows up as a changed line. Types: kind, modifiers, base type, interfaces,
/// enum values. Members (public and protected): modifiers, types with their nullability, parameter names and
/// defaults, <c>params</c>, <c>ref</c>/<c>out</c>/<c>in</c>, <c>init</c> setters, constant values, and
/// <c>[Obsolete]</c>.
/// </summary>
internal static class PublicSurface
{
    #region Fields and Constants

    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly NullabilityInfoContext Nullability = new();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the surface of <paramref name="assembly"/>.
    /// </summary>
    public static string Describe(Assembly assembly)
    {
        var lines = new List<string>();
        foreach (var type in assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            lines.Add(TypeLine(type));
            var members = new List<string>();
            if (type.IsEnum)
            {
                foreach (var name in Enum.GetNames(type))
                {
                    members.Add($"  {name} = {Convert.ToInt64(Enum.Parse(type, name), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)}");
                }
            }
            else
            {
                foreach (var member in type.GetMembers(Declared).Where(IsVisible))
                {
                    if (member is MethodInfo { IsSpecialName: true }) continue; // property and event accessors
                    members.Add("  " + MemberLine(member));
                }
            }
            lines.AddRange(members.OrderBy(m => m, StringComparer.Ordinal));
        }
        return string.Join("\n", lines);
    }

    #endregion

    #region Private Methods

    private static string Accessibility(MethodBase method) => method.IsPublic ? "public" : "protected";

    private static string Attributes(MemberInfo member) =>
        member.GetCustomAttribute<ObsoleteAttribute>() is { } obsolete ? $"[Obsolete({obsolete.Message})] " : string.Empty;

    private static bool IsVisible(MemberInfo member) => member switch
    {
        MethodBase method => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly,
        FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
        PropertyInfo property => property.GetAccessors(true).Any(a => a.IsPublic || a.IsFamily || a.IsFamilyOrAssembly),
        EventInfo e => e.AddMethod is { } add && (add.IsPublic || add.IsFamily || add.IsFamilyOrAssembly),
        Type nested => nested.IsNestedPublic || nested.IsNestedFamily || nested.IsNestedFamORAssem,
        _ => false,
    };

    private static string MemberLine(MemberInfo member) => Attributes(member) + member switch
    {
        ConstructorInfo constructor => $"{Accessibility(constructor)} {(constructor.IsStatic ? "static " : "")}.ctor({Parameters(constructor)})",
        MethodInfo method => $"{Accessibility(method)} {MethodModifiers(method)}{TypeName(method.ReturnType, Nullability.Create(method.ReturnParameter))} {method.Name}{GenericArguments(method)}({Parameters(method)})",
        PropertyInfo property => PropertyLine(property),
        FieldInfo field => $"{(field.IsPublic ? "public" : "protected")} {(field.IsLiteral ? "const " : field.IsStatic ? "static " : "")}{(field.IsInitOnly ? "readonly " : "")}{TypeName(field.FieldType, Nullability.Create(field))} {field.Name}{(field.IsLiteral ? " = " + Literal(field.GetRawConstantValue()) : "")}",
        EventInfo e => $"public event {TypeName(e.EventHandlerType!, null)} {e.Name}",
        Type nested => "nested " + nested.Name,
        _ => member.ToString()!,
    };

    private static string GenericArguments(MethodInfo method) =>
        method.IsGenericMethodDefinition ? "<" + string.Join(", ", method.GetGenericArguments().Select(a => a.Name)) + ">" : string.Empty;

    private static string Literal(object? value) => value switch
    {
        null => "null",
        string text => "\"" + text + "\"",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()!,
    };

    private static string MethodModifiers(MethodInfo method)
    {
        if (method.IsStatic) return "static ";
        if (method.IsAbstract) return "abstract ";
        if (method.IsVirtual && !method.IsFinal) return method.GetBaseDefinition() != method ? "override " : "virtual ";
        return method.IsVirtual && method.GetBaseDefinition() != method ? "sealed override " : string.Empty;
    }

    private static string Parameters(MethodBase method) => string.Join(", ", method.GetParameters().Select(p =>
    {
        string prefix = p.IsOut ? "out " : p.ParameterType.IsByRef ? (p.IsIn ? "in " : "ref ") : p.GetCustomAttribute<ParamArrayAttribute>() is not null ? "params " : "";
        var type = p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType;
        string defaultValue = p.HasDefaultValue ? " = " + Literal(p.RawDefaultValue) : string.Empty;
        return $"{prefix}{TypeName(type, Nullability.Create(p))} {p.Name}{defaultValue}";
    }));

    private static string PropertyLine(PropertyInfo property)
    {
        var getter = property.GetMethod;
        var setter = property.SetMethod;
        bool isStatic = (getter ?? setter)!.IsStatic;
        var accessors = new List<string>();
        if (getter is not null && IsVisible(getter)) accessors.Add($"{Accessibility(getter)} get");
        if (setter is not null && IsVisible(setter))
        {
            bool init = setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));
            accessors.Add($"{Accessibility(setter)} {(init ? "init" : "set")}");
        }
        string indexer = property.GetIndexParameters().Length > 0
            ? "[" + string.Join(", ", property.GetIndexParameters().Select(p => $"{TypeName(p.ParameterType, Nullability.Create(p))} {p.Name}")) + "]"
            : string.Empty;
        string modifiers = isStatic ? "static " : getter is { } g ? MethodModifiers(g) : string.Empty;
        return $"{modifiers}{TypeName(property.PropertyType, Nullability.Create(property))} {property.Name}{indexer} {{ {string.Join("; ", accessors)} }}";
    }

    private static string TypeLine(Type type)
    {
        string kind = type.IsEnum ? "enum" : type.IsInterface ? "interface" : type.IsValueType ? "struct" : "class";
        string modifiers = type.IsAbstract && type.IsSealed ? "static " : type.IsAbstract && !type.IsInterface ? "abstract " : type.IsSealed && !type.IsValueType && !type.IsEnum ? "sealed " : "";
        var bases = new List<string>();
        if (type.IsEnum) bases.Add(Enum.GetUnderlyingType(type).Name);
        else if (type.BaseType is { } baseType && baseType != typeof(object) && baseType != typeof(ValueType)) bases.Add(TypeName(baseType, null));
        bases.AddRange(type.GetInterfaces().Select(i => TypeName(i, null)).OrderBy(n => n, StringComparer.Ordinal));
        return $"{Attributes(type)}{modifiers}{kind} {type.FullName}{(bases.Count > 0 ? " : " + string.Join(", ", bases) : "")}";
    }

    private static string TypeName(Type type, NullabilityInfo? nullability)
    {
        string name;
        if (type.IsGenericType)
        {
            string definition = type.GetGenericTypeDefinition().FullName ?? type.Name;
            definition = definition.Substring(0, definition.IndexOf('`'));
            var arguments = type.GetGenericArguments();
            var argumentNullability = nullability?.GenericTypeArguments;
            name = definition + "<" + string.Join(", ", arguments.Select((a, i) =>
                TypeName(a, argumentNullability is { Length: > 0 } && i < argumentNullability.Length ? argumentNullability[i] : null))) + ">";
        }
        else if (type.IsArray)
        {
            name = TypeName(type.GetElementType()!, nullability?.ElementType) + "[]";
        }
        else
        {
            name = type.FullName ?? type.Name;
        }
        return nullability is { ReadState: NullabilityState.Nullable } && !type.IsValueType ? name + "?" : name;
    }

    #endregion

    #endregion
}
