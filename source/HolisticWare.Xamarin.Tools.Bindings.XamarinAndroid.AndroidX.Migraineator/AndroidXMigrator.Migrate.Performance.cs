using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;

using Core.Text;
using Mono.Cecil.Rocks;
using Mono.Cecil;

namespace HolisticWare.Xamarin.Tools.Bindings.XamarinAndroid.AndroidX.Migraineator
{
    public partial class AndroidXMigrator
    {
        partial void MigrateWithSpanMemory();

        /// <summary>
        /// 
        /// </summary>
        /// https://gist.github.com/Redth/c8c3e4999ea33b80950aae3095f7839b
        partial void MigrateWithSpanMemory()
        {
            asm_def = Mono.Cecil.AssemblyDefinition.ReadAssembly
                                                        (
                                                            this.PathAssemblyInput,
                                                            new Mono.Cecil.ReaderParameters
                                                                {
                                                                    AssemblyResolver = CreateAssemblyResolver(),
                                                                    ReadWrite = true,
                                                                    InMemory = true 
                                                                }
                                                        );

            Mono.Collections.Generic.Collection<Mono.Cecil.TypeDefinition> types_plain = null;
            IEnumerable<Mono.Cecil.TypeDefinition> types_rocks = null;

            types_rocks = asm_def.MainModule.GetAllTypes();
            types_plain = asm_def.MainModule.Types;


            Trace.WriteLine($"===================================================================================");
            Trace.WriteLine($" migrating assembly                       = {this.PathAssemblyInput}");
            Trace.WriteLine($"                 types Mono.Cecil       # = {types_plain.Count()}");
            Trace.WriteLine($"                 types Mono.Cecil.Rocks # = {types_rocks.Count()}");

            Stopwatch timer = new Stopwatch();
            timer.Start();

            foreach (Mono.Cecil.TypeDefinition t in types_rocks)
            {
                ProcessType(t);
            }

            timer.Stop();

            long ms = timer.ElapsedMilliseconds;

            Trace.WriteLine($"Assembly processing {ms}ms");
            Trace.WriteLine($"===================================================================================");

            asm_def.Write(this.PathAssemblyOutput);

            return;
        }

        private void ProcessType(TypeDefinition t)
        {
            Trace.WriteLine($"    processing Type");
            Trace.WriteLine($"                       Name        = {t.Name}");
            Trace.WriteLine($"                       FullName    = {t.FullName}");
            Trace.WriteLine($"                       IsClass     = {t.IsClass}");
            Trace.WriteLine($"                       IsInterface = {t.IsInterface}");

            ProcessAttributes(t.Attributes);
            ProcessMethods(t.GetMethods());

            return;
        }

        private void ProcessAttributes(TypeAttributes attributes)
        {
            Trace.WriteLine($"                            Attribute = {attributes.ToString()}");

            return;
        }

        private void ProcessMethods(IEnumerable<MethodDefinition> methods)
        {
            foreach (Mono.Cecil.MethodDefinition method in methods)
            {
                Trace.WriteLine($"                        Method = {method.Name}");

                ReadOnlyMemory<char> memory_return_type_full_name;
                string return_type_full_name = method.MethodReturnType?.ReturnType?.FullName;

                bool has_support = false;

                if (null != return_type_full_name)
                {
                    memory_return_type_full_name = return_type_full_name.AsMemory();
                    has_support = memory_return_type_full_name.Span.StartsWith(memory_android_support.Span);
                    Trace.WriteLine($"                       returns  = {return_type_full_name}");
                    if(has_support)
                    {
                        int start = memory_android_support.Span.Length;
                        int length = memory_return_type_full_name.Span.Length - start;
                        ReadOnlySpan<char> span_androidx = memory_android_support.Span;
                        ReadOnlySpan<char> replaced = memory_return_type_full_name.Span.Slice(start, length);
                    }
                }

                bool has_support_parameters = ProcessMethodparameters(method);
                has_support = has_support || has_support_parameters;


                string registerAttrMethodName = null;
                string registerAttributeJniSig = null;
                string registerAttributeNewJniSig = null;

                bool isBindingMethod = false;

                foreach (Mono.Cecil.CustomAttribute attr in method.CustomAttributes)
                {

                    if (attr.AttributeType.FullName.Equals("Android.Runtime.RegisterAttribute"))
                    {
                        Mono.Cecil.CustomAttributeArgument jniSigArg = attr.ConstructorArguments[1];

                        registerAttrMethodName = attr.ConstructorArguments[0].Value.ToString();
                        registerAttributeJniSig = jniSigArg.Value.ToString();

                        registerAttributeNewJniSig = ReplaceJniSignature(registerAttributeJniSig);

                        attr.ConstructorArguments[1] = new Mono.Cecil.CustomAttributeArgument(jniSigArg.Type, registerAttributeNewJniSig);

                        isBindingMethod = true;

                        Trace.WriteLine($"[Register(\"{attr.ConstructorArguments[0].Value}\", \"{registerAttributeNewJniSig}\")]");
                    }
                }

                if (!isBindingMethod)
                    return;

                if (method.HasBody)
                {
                    // Replace all the JNI Signatures inside the method body
                    foreach (var instr in method.Body.Instructions)
                    {
                        if (instr.OpCode.Name == "ldstr")
                        {
                            var jniSig = instr.Operand.ToString();

                            var indexOfDot = jniSig.IndexOf('.');

                            // New binding glue style is `methodName.(Lparamater/Type;)Lreturn/Type;`
                            if (indexOfDot >= 0)
                            {
                                var methodName = jniSig.Substring(0, indexOfDot);
                                var newJniSig = ReplaceJniSignature(jniSig.Substring(indexOfDot + 1));
                                instr.Operand = $"{methodName}.{newJniSig}";

                                Trace.WriteLine($"{methodName} -> {newJniSig}");
                            }
                            // Old style is two strings, one with method name and then `(Lparameter/Type;)Lreturn/Type;`
                            else if (jniSig.Contains('(') && jniSig.Contains(')'))
                            {
                                var methodName = instr.Previous.Operand.ToString();
                                var newJniSig = ReplaceJniSignature(jniSig);

                                instr.Operand = newJniSig;

                                Trace.WriteLine($"{methodName} -> {newJniSig}");
                            }
                        }
                    }
                }

            }
        }

        private static bool ProcessMethodparameters(MethodDefinition method)
        {
            bool has_support = false;

            if (method.HasParameters)
            {
                foreach (ParameterDefinition method_parameter in method.Parameters)
                {
                    ReadOnlySpan<char> span_method_parameter_namespace;
                    string method_parameter_namespace = method_parameter.ParameterType.Namespace;
                    span_method_parameter_namespace = method_parameter_namespace.AsSpan();

                    bool has_support_parameter = span_method_parameter_namespace.StartsWith(memory_android_support.Span);
                    has_support = has_support || has_support_parameter;

                    if (has_support)
                    {
                        Trace.WriteLine($"                       parameter  = {method_parameter.ParameterType.Name}");
                        break;
                    }
                }
            }

            return has_support;
        }

    }
}
