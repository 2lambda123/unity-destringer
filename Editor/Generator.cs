using System;
using UnityEngine;
using System.Text;

using ParameterType = UnityEngine.AnimatorControllerParameterType;
using Parameter = UnityEngine.AnimatorControllerParameter;
using Controller = UnityEngine.RuntimeAnimatorController;

namespace Destringer {
  public enum AccessModifier {
    Public,
    Internal,
    Protected,
    Private,
  }

  public static class Generator {
    const int TabSize = 4;
    const string AnimatorFieldName = "_animator";
    const string GeneratedWarning = @"
/*******************************************************************************
 *                             !!! WARNING !!!                                 *
 *                                                                             *
 *         Do not modify this file--any changes will be overwritten.           *
 *                This file was generated by AnimatorWrapper.                  *
 ******************************************************************************/
";

    internal static string GenerateFromController(
      Controller controller,
      string namespaceName,
      string className,
      AccessModifier accessModifier,
      bool isPartial
    ) {
      Assert(controller != null, $"{nameof(controller)} == null");
      Assert(className != null, $"{nameof(className)} == null");
      return Generate(
        controller,
        namespaceName,
        className,
        accessModifier,
        isPartial
      );
    }

    static Parameter[] GetParameters(Controller controller) {
      GameObject gameObject = null;
      try {
        gameObject = new GameObject() { hideFlags = HideFlags.HideAndDontSave };
        var animator = gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        return animator.parameters;
      } finally {
        if (gameObject != null) GameObject.DestroyImmediate(gameObject);
      }
    }

    static string Generate(
      Controller controller,
      string namespaceName,
      string className,
      AccessModifier accessModifier,
      bool isPartial
    ) {
      var parameters = GetParameters(controller);
      var indent = 0;
      if (className == null) {
        className = ClassName(controller);
      }
      var hasNamespace = !string.IsNullOrWhiteSpace(namespaceName);

      var sb = new StringBuilder();

      sb.Append(GeneratedWarning);
      BlankLine(sb);
      AddImports(
        sb,
        ref indent,
        nameof(System),
        nameof(UnityEngine),
        $"{nameof(ParameterType)} = UnityEngine.AnimatorControllerParameterType"
      );
      BlankLine(sb);
      if (hasNamespace) OpenNamespace(sb, ref indent, namespaceName);
      {
        OpenClass(sb, ref indent, className, isPartial, isPartial ? null : "MonoBehaviour");
        {
          AddConstants(sb, ref indent, parameters);
          BlankLine(sb);
          AddAnimatorField(sb, ref indent, accessModifier);
          BlankLine(sb);
          AddStart(sb, ref indent, controller.name, parameters);
          BlankLine(sb);
          AddMembers(sb, ref indent, accessModifier, parameters);
          BlankLine(sb);
          AddIsCompatible(sb, ref indent, parameters);
          BlankLine(sb);
          AddReset(sb, ref indent, controller.name);
        }
        CloseBlock(sb, ref indent);
      }
      if (hasNamespace) CloseBlock(sb, ref indent);

      return sb.ToString();
    }

    // General purpose generation.

    static string ClassName(Controller controller) {
      return controller.name;
    }

    static void AddAttribute(StringBuilder sb, int indent, string attribute) {
      NewLine(sb, indent);
      sb.Append('[');
      sb.Append(attribute);
      sb.Append(']');
      sb.AppendLine();
    }

    static void AddAccessModifier(StringBuilder sb, AccessModifier accessModifier) {
      switch (accessModifier) {
        case AccessModifier.Public: sb.Append("public"); break;
        case AccessModifier.Protected: sb.Append("protected"); break;
        case AccessModifier.Private: sb.Append("private"); break;
        case AccessModifier.Internal: sb.Append("internal"); break;
        default: throw Never($"Unexpected access modifier {accessModifier}");
      }
    }

    static void AddAnimatorField(StringBuilder sb, ref int indent, AccessModifier accessModifier) {
      AddAttribute(sb, indent, "Header(\"AnimatorWrapper\")");
      AddAttribute(sb, indent, "Tooltip(\"The animator bound to code generated by AnimatorWrapper\")");
      AddAttribute(sb, indent, "SerializeField");
      NewLine(sb, indent);
      sb.Append("Animator ");
      sb.Append(AnimatorFieldName);
      sb.AppendLine(" = null;");

      BlankLine(sb);

      NewLine(sb, indent);
      AddAccessModifier(sb, accessModifier);
      sb.Append(" Animator Animator ");
      OpenBlock(sb, ref indent);
      {
        NewLine(sb, indent);
        sb.Append("get => ");
        sb.Append(AnimatorFieldName);
        sb.Append(';');
        sb.AppendLine();

        NewLine(sb, indent);
        sb.Append("set { ");
        sb.Append(AnimatorFieldName);
        sb.AppendLine(" = value; }");
      }
      CloseBlock(sb, ref indent);
    }

    static void NewLine(StringBuilder sb, int indent) {
      for (int i = 0; i < indent * TabSize; i++) {
        sb.Append(' ');
      }
    }

    static void BlankLine(StringBuilder sb) {
      NewLine(sb, 0);
      sb.AppendLine();
    }

    static void AddImports(StringBuilder sb, ref int indent, params string[] imports) {
      foreach (var import in imports) {
        NewLine(sb, indent);
        sb.Append("using ");
        sb.Append(import);
        sb.AppendLine(";");
      }
    }

    static void OpenNamespace(StringBuilder sb, ref int indent, string name) {
      Assert(!string.IsNullOrEmpty(name), "namespace name empty");
      NewLine(sb, indent);
      sb.Append("namespace ");
      sb.Append(name);
      OpenBlock(sb, ref indent);
    }

    static void OpenClass(StringBuilder sb, ref int indent, string className, bool isPartial, string baseClassName) {
      Assert(!string.IsNullOrWhiteSpace(className), "class name empty");
      NewLine(sb, indent);
      if (isPartial) {
        sb.Append("partial ");
      } else {
        // No need to specify an access modifier for a partial class. We just
        // omit it and let the consumer add it to their class.
        AddAccessModifier(sb, AccessModifier.Public);
        sb.Append(' ');
      }
      sb.Append("class ");
      sb.Append(className);
      if (!string.IsNullOrWhiteSpace(baseClassName)) {
        sb.Append(" : ");
        sb.Append(baseClassName);
      }
      OpenBlock(sb, ref indent);
    }

    static void OpenBlock(StringBuilder sb, ref int indent) {
      sb.AppendLine();
      NewLine(sb, indent);
      sb.AppendLine("{");
      indent++;
    }

    static void CloseBlock(StringBuilder sb, ref int indent) {
      indent--;
      NewLine(sb, indent);
      sb.AppendLine("}");
    }

    // Member generation.

    static void AddStart(StringBuilder sb, ref int indent, string controllerName, Parameter[] parameters) {
      NewLine(sb, indent); sb.Append("protected void Start()");
      OpenBlock(sb, ref indent);

      // Only check in editor.
      NewLine(sb, indent); sb.AppendLine("#if UNITY_EDITOR");

      // Throw error when invalid.
      NewLine(sb, indent);
      sb.Append("if (!IsCompatible(_animator)) ");
      OpenThrow(sb, ref indent);
      sb.Append("\"AnimatorWrapper is out of sync with ");
      sb.Append(nameof(RuntimeAnimatorController));
      sb.Append(". Check that you have '");
      sb.Append(controllerName);
      sb.AppendLine("' assigned or regenerate the wrapper.\"");
      CloseThrow(sb, ref indent);

      // Close `#if UNITY_EDITOR`.
      NewLine(sb, indent); sb.AppendLine("#endif");

      // Close `Start`.
      CloseBlock(sb, ref indent);
    }

    static void AddReset(StringBuilder sb, ref int indent, string controllerName) {
      NewLine(sb, indent);
      sb.Append($"void Reset()");
      OpenBlock(sb, ref indent);
      {

        NewLine(sb, indent); sb.AppendLine("var animators = GetComponentsInChildren<Animator>();");
        NewLine(sb, indent);
        sb.Append("foreach (var animator in animators)");
        OpenBlock(sb, ref indent);
        {
          NewLine(sb, indent);
          sb.Append("if (IsCompatible(animator))");
          OpenBlock(sb, ref indent);
          {
            NewLine(sb, indent);
            sb.Append(AnimatorFieldName);
            sb.AppendLine(" = animator;");

            NewLine(sb, indent);
            sb.AppendLine("return;");
          }
          // Close `if`
          CloseBlock(sb, ref indent);
        }
        // Close `foreach`
        CloseBlock(sb, ref indent);

        NewLine(sb, indent);
        sb.Append("Debug.LogWarning(\"Unable to find animator with compatible ");
        sb.Append(nameof(RuntimeAnimatorController));
        sb.AppendLine(" in GameObject\", this);");
      }
      CloseBlock(sb, ref indent);
    }

    static void AddIsCompatible(StringBuilder sb, ref int indent, Parameter[] parameters) {
      const string ParamName = "animator";
      NewLine(sb, indent);
      sb.Append($"static bool IsCompatible({nameof(Animator)} {ParamName})");
      OpenBlock(sb, ref indent);

      NewLine(sb, indent);
      sb.AppendLine("return (");
      indent++;

      // Check controller
      NewLine(sb, indent);
      sb.Append(ParamName);
      sb.Append('.');
      sb.Append(nameof(Animator.runtimeAnimatorController));
      sb.Append(" != null");
      sb.AppendLine(" && ");

      // Check parameter count.
      NewLine(sb, indent);
      sb.Append(ParamName);
      sb.Append(".parameterCount == ");
      sb.Append(parameters.Length);
      if (parameters.Length > 0) {
        sb.AppendLine(" && ");
      }

      // Check each parameter.
      for (int i = 0; i < parameters.Length; i++) {

        // Check parameter type.
        NewLine(sb, indent);
        sb.Append(ParamName);
        sb.Append(".GetParameter(");
        sb.Append(i);
        sb.Append(").type == ");
        sb.Append(nameof(ParameterType));
        sb.Append('.');
        sb.Append(parameters[i].type);
        sb.AppendLine(" && ");

        // Check parameter name hash.
        NewLine(sb, indent);
        sb.Append(ParamName);
        sb.Append(".GetParameter(");
        sb.Append(i);
        sb.Append(").nameHash == ");
        AddConstantName(sb, parameters[i]);

        if (i < parameters.Length - 1) {
          sb.AppendLine(" && ");
        }
      }

      // Close `return`.
      sb.AppendLine();
      indent--;
      NewLine(sb, indent);
      sb.AppendLine(");");

      CloseBlock(sb, ref indent);
    }

    static void OpenThrow(StringBuilder sb, ref int indent) {
      sb.AppendLine("throw new InvalidOperationException(");
      indent++;
      NewLine(sb, indent);
    }

    static void CloseThrow(StringBuilder sb, ref int indent) {
      indent--;
      NewLine(sb, indent);
      sb.AppendLine(");");
    }

    static void AddConstants(StringBuilder sb, ref int indent, Parameter[] parameters) {
      for (int i = 0; i < parameters.Length; i++) {
        AddConstant(sb, ref indent, parameters[i]);
      }
    }

    static void AddConstantName(StringBuilder sb, Parameter parameter) {
      sb.Append(StringUtility.PascalCase(parameter.name));
      sb.Append("Property");
    }

    static void AddConstant(StringBuilder sb, ref int indent, Parameter parameter) {
      NewLine(sb, indent);
      sb.Append("const int ");
      AddConstantName(sb, parameter);
      sb.Append(" = ");
      sb.Append(parameter.nameHash);
      sb.AppendLine(";");
    }

    static void AddMembers(StringBuilder sb, ref int indent, AccessModifier accessModifier, Parameter[] parameters) {
      for (int i = 0; i < parameters.Length; i++) {
        AddMember(sb, ref indent, accessModifier, parameters[i]);
        if (i < parameters.Length - 1) {
          BlankLine(sb);
        }
      }
    }

    static void AddMember(StringBuilder sb, ref int indent, AccessModifier accessModifier, Parameter parameter) {
      switch (parameter.type) {
        case ParameterType.Bool:
        case ParameterType.Float:
        case ParameterType.Int:
          AddProperty(sb, ref indent, accessModifier, parameter);
          break;
        case ParameterType.Trigger:
          AddTrigger(sb, ref indent, accessModifier, parameter);
          break;
        default:
          Debug.LogError($"Unsupported parameter type: {parameter.type}");
          break;
      }
    }

    static string ParameterTypeToAccessorName(ParameterType type) {
      switch (type) {
        case ParameterType.Int: return "Integer";
        default: return type.ToString();
      }
    }

    static void AddProperty(StringBuilder sb, ref int indent, AccessModifier accessModifier, Parameter parameter) {
      NewLine(sb, indent);
      AddAccessModifier(sb, accessModifier);
      sb.Append(' ');
      sb.Append(ParameterTypeToType(parameter.type));
      sb.Append(' ');
      sb.Append(StringUtility.PascalCase(parameter.name));
      OpenBlock(sb, ref indent);
      {
        NewLine(sb, indent);
        sb.Append("get => ");
        sb.Append(AnimatorFieldName);
        sb.Append(".Get");
        sb.Append(ParameterTypeToAccessorName(parameter.type));
        sb.Append('(');
        AddConstantName(sb, parameter);
        sb.AppendLine(");");

        NewLine(sb, indent);
        sb.Append("set { ");
        sb.Append(AnimatorFieldName);
        sb.Append(".Set");
        sb.Append(ParameterTypeToAccessorName(parameter.type));
        sb.Append('(');
        AddConstantName(sb, parameter);
        sb.AppendLine(", value); }");
      }
      CloseBlock(sb, ref indent);
    }

    static void AddTrigger(StringBuilder sb, ref int indent, AccessModifier accessModifier, Parameter parameter) {
      NewLine(sb, indent);
      AddAccessModifier(sb, accessModifier);
      sb.Append(" void ");
      sb.Append(parameter.name);
      sb.Append("()");
      OpenBlock(sb, ref indent);

      NewLine(sb, indent);
      sb.Append(AnimatorFieldName);
      sb.Append(".SetTrigger(");
      AddConstantName(sb, parameter);
      sb.AppendLine(");");

      CloseBlock(sb, ref indent);
    }

    static string ParameterTypeToType(ParameterType type) {
      switch (type) {
        case ParameterType.Int: return "int";
        case ParameterType.Float: return "float";
        case ParameterType.Bool: return "bool";
        default: throw Never($"Unexpected parameter type {type}");
      }
    }

    // Debug helpers.

    static void Assert(bool condition, string message) {
      if (!condition) throw Never($"Assertion failed: #{message}");
    }

    static InvalidOperationException Never(string message) {
      return new InvalidOperationException(message);
    }
  }
}
