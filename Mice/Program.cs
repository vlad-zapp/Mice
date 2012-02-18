using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Diagnostics;
using StrongNameKeyPair = System.Reflection.StrongNameKeyPair;
using System.IO;

namespace Mice
{
    static class Program
    {
        #region main
        static int Main(string[] args)
        {
            if (args.Length < 1)
                return Using();

            string victimName = args[0];
            string keyFile = args.Length > 1 ? args[1] : null;

            //try
            //{
            var assembly = AssemblyDefinition.ReadAssembly(victimName);
            foreach (var type in assembly.Modules.SelectMany(m => m.Types).Where(IsTypeToBeProcessed).ToArray())
                ProcessType(type);

            var writerParams = new WriterParameters();
            if (!string.IsNullOrEmpty(keyFile) && File.Exists(keyFile))
                writerParams.StrongNameKeyPair = new StrongNameKeyPair(File.ReadAllBytes(keyFile));

            assembly.Write(victimName, writerParams);
            return 0;
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine("Error. " + e.ToString());
            //    Console.ReadKey();
            //    return 1;
            //}
        }
        private static int Using()
        {
            Console.WriteLine("Usage: mice.exe assembly-name.dll [key-file.snk]");
            return 1;
        }
        #endregion

        #region checkings

        private static bool IsTypeToBeProcessed(TypeDefinition type)
        {
            return type.IsPublic &&
                !type.IsEnum &&
                !type.IsValueType &&
                !type.IsInterface &&
                type.BaseType.Name != "MulticastDelegate";
        }

        private static bool IsMethodToBeProcessed(MethodDefinition m)
        {
            return (m.IsPublic) &&
                m.GenericParameters.Count == 0 &&
                !m.IsAbstract &&
                !(m.IsStatic && m.IsConstructor);
        }

        #endregion

        private static void ProcessType(TypeDefinition type)
        {
            TypeDefinition prototypeType = CreatePrototypeType(type);

            FieldDefinition prototypeField = new FieldDefinition(type.Name.Replace('`', '_') + "Prototype", FieldAttributes.Public, prototypeType);
            type.Fields.Add(prototypeField);
            MakeGenericField(prototypeField);

            FieldDefinition staticPrototypeField = new FieldDefinition("StaticPrototype", FieldAttributes.Public | FieldAttributes.Static, prototypeType);
            type.Fields.Add(staticPrototypeField);
            MakeGenericField(staticPrototypeField);

            //create delegate types & fields, patch methods to call delegates
            var processingMethods = type.Methods.Where(IsMethodToBeProcessed).ToArray();
            foreach (var method in processingMethods)
            {
                if (!(method.IsConstructor || method.IsGetter || method.IsSetter))
                {
                    CreateDeligateType(method);
                    CreateDeligateField(method);

                    MethodDefinition newMethod = MoveCodeToImplMethod(method);

                    //AddStaticPrototypeCall(method, delegateField, staticPrototypeField);

                    if (!method.IsStatic)
                    {
                        //AddInstancePrototypeCall(method, delegateField, prototypeField);
                    }
                }
            }

            if (type.HasGenericParameters) return; //generics processing stop here))

            //After using of Mice there always should be a wasy to create an instance of public class
            //Here we create methods that can call parameterless ctor, evern if there is no parameterless ctor :)
            if (!type.IsAbstract)
            {
                var privateDefaultCtor =
                    type.Methods.SingleOrDefault(m => m.IsConstructor && m.Parameters.Count == 0 && !m.IsPublic && !m.IsStatic);

                if (privateDefaultCtor != null)
                {
                    var delegateType = CreateDeligateType(privateDefaultCtor);
                    var delegateField = CreateDeligateField(privateDefaultCtor);

                    MethodDefinition newMethod = MoveCodeToImplMethod(privateDefaultCtor);
                    AddStaticPrototypeCall(privateDefaultCtor, delegateField, staticPrototypeField);

                    CreateCallToPrivateCtor(privateDefaultCtor, prototypeType);
                }
                else
                {
                    var publicDefaultCtor =
                        type.Methods.SingleOrDefault(m => m.IsConstructor && m.Parameters.Count == 0 && m.IsPublic && !m.IsStatic);
                    if (publicDefaultCtor == null) //there is not default ctor, neither private nor public
                    {
                        privateDefaultCtor = CreateDefaultCtor(type);
                        CreateCallToPrivateCtor(privateDefaultCtor, prototypeType);
                    }
                }
            }
        }

        private static MethodDefinition CreateDefaultCtor(TypeDefinition type)
        {
            //create constructor
            var constructor = new MethodDefinition(".ctor",
                MethodAttributes.Private | MethodAttributes.CompilerControlled |
                MethodAttributes.RTSpecialName | MethodAttributes.SpecialName |
                MethodAttributes.HideBySig, type.Module.Import(typeof(void)));
            type.Methods.Add(constructor);
            constructor.Body.GetILProcessor().Emit(OpCodes.Ret);

            return constructor;
        }


        private static MethodDefinition CreateCallToPrivateCtor(MethodDefinition defCtor, TypeDefinition prototypeType)
        {
            MethodDefinition result = new MethodDefinition("CallCtor", MethodAttributes.Public, defCtor.DeclaringType);
            var il = result.Body.GetILProcessor();
            il.Emit(OpCodes.Newobj, defCtor);
            il.Emit(OpCodes.Ret);

            prototypeType.Methods.Add(result);

            return result;
        }

        private static MethodDefinition MoveCodeToImplMethod(MethodDefinition method)
        {
            const string realImplementationPrefix = "x";
            string name;
            if (method.IsConstructor)
                name = realImplementationPrefix + "Ctor";
            else if (method.IsSetter && method.Name.StartsWith("set_"))
                name = "set_" + realImplementationPrefix + method.Name.Substring(4);
            else if (method.IsGetter && method.Name.StartsWith("get_"))
                name = "get_" + realImplementationPrefix + method.Name.Substring(4);
            else
                name = realImplementationPrefix + method.Name;

            MethodDefinition result = new MethodDefinition(name, method.Attributes, method.ReturnType);
            result.SemanticsAttributes = method.SemanticsAttributes;

            result.IsRuntimeSpecialName = false;
            result.IsVirtual = false;
            if (method.IsConstructor)
                result.IsSpecialName = false;

            foreach (var param in method.Parameters)
            {
                result.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
            }

            //copy method's body to x-method           
            result.Body = method.Body;
            method.Body = new MethodBody(method);

            //add x-method to a type
            method.DeclaringType.Methods.Add(result);

            //registering a property if it's needed
            if (result.IsGetter || result.IsSetter)
            {
                TypeReference propertyType = result.IsGetter ? result.ReturnType : result.Parameters[0].ParameterType;
                string propertyName = result.Name.Substring(4);
                var property = method.DeclaringType.Properties.FirstOrDefault(p => p.Name == propertyName);
                if (property == null)
                {
                    property = new PropertyDefinition(propertyName, PropertyAttributes.None, propertyType);
                    method.DeclaringType.Properties.Add(property);
                }
                if (result.IsGetter)
                    property.GetMethod = result;
                else
                    property.SetMethod = result;
            }

            //repalce old method body
            var il = method.Body.GetILProcessor();

            int allParamsCount = method.Parameters.Count + (method.IsStatic ? 0 : 1); //all params and maybe this
            for (int i = 0; i < allParamsCount; i++)
                il.Emit(OpCodes.Ldarg, i);

            //result.MakeGeneric(method.DeclaringType.GenericParameters);
            il.Emit(OpCodes.Call, result.DeclaringType.HasGenericParameters?result.MakeGeneric(method.DeclaringType.GenericParameters):result);

            il.Emit(OpCodes.Ret);
            return result;
        }

        private static void AddStaticPrototypeCall(MethodDefinition method, FieldDefinition delegateField, FieldDefinition prototypeField)
        {
            Debug.Assert(prototypeField.IsStatic);
            var firstOpcode = method.Body.Instructions.First();
            var il = method.Body.GetILProcessor();

            TypeDefinition delegateType = delegateField.FieldType.Resolve();
            var invokeMethod = delegateType.Methods.Single(m => m.Name == "Invoke");
            int allParamsCount = method.Parameters.Count + (method.IsStatic ? 0 : 1); //all params and maybe this

            var instructions = new[]
			{
				il.Create(OpCodes.Ldsflda, prototypeField),
				il.Create(OpCodes.Ldfld, delegateField),
				il.Create(OpCodes.Brfalse, firstOpcode),

				il.Create(OpCodes.Ldsflda, prototypeField),
				il.Create(OpCodes.Ldfld, delegateField),
			}.Concat(
                Enumerable.Range(0, allParamsCount).Select(i => il.Create(OpCodes.Ldarg, i))
            ).Concat(new[]
			{
				il.Create(OpCodes.Callvirt, invokeMethod),
				il.Create(OpCodes.Ret),
			});

            foreach (var instruction in instructions)
                il.InsertBefore(firstOpcode, instruction);
        }

        private static void AddInstancePrototypeCall(MethodDefinition method, FieldDefinition delegateField, FieldDefinition prototypeField)
        {
            Debug.Assert(!prototypeField.IsStatic);
            var firstOpcode = method.Body.Instructions.First();
            var il = method.Body.GetILProcessor();

            TypeDefinition delegateType = delegateField.FieldType.Resolve();
            var invokeMethod = delegateType.Methods.Single(m => m.Name == "Invoke");
            int allParamsCount = method.Parameters.Count + 1; //all params and this

            var instructions = new[]
			{
				il.Create(OpCodes.Ldarg_0),
				il.Create(OpCodes.Ldflda, prototypeField),
				il.Create(OpCodes.Ldfld, delegateField),
				il.Create(OpCodes.Brfalse, firstOpcode),

				il.Create(OpCodes.Ldarg_0),
				il.Create(OpCodes.Ldflda, prototypeField),
				il.Create(OpCodes.Ldfld, delegateField),
			}.Concat(
                Enumerable.Range(0, allParamsCount).Select(i => il.Create(OpCodes.Ldarg, i))
            ).Concat(new[]
			{
				il.Create(OpCodes.Callvirt, invokeMethod),
				il.Create(OpCodes.Ret),
			});

            foreach (var instruction in instructions)
                il.InsertBefore(firstOpcode, instruction);
        }

        private static TypeDefinition CreatePrototypeType(TypeDefinition type)
        {
            TypeDefinition result = new TypeDefinition(null, "PrototypeClass",
                TypeAttributes.Sealed | TypeAttributes.NestedPublic | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout,
                type.Module.Import(typeof(ValueType)));

            type.NestedTypes.Add(result);
            result.DeclaringType = type;
            MakeGenericType(result);
            return result;
        }

        private static FieldDefinition CreateDeligateField(MethodDefinition method)
        {
            var fieldName = ComposeFullMethodName(method);
            var delegateType = method.DeclaringType.NestedTypes.First(m => m.Name == "PrototypeClass").
                NestedTypes.First(m => m.Name == "Callback_" + fieldName);
            FieldDefinition field = new FieldDefinition(fieldName, FieldAttributes.Public, delegateType);
            method.DeclaringType.Fields.Add(field);
            MakeGenericField(field);
            return field;
        }

        private static TypeDefinition CreateDeligateType(MethodDefinition method)
        {
            string deligateName = "Callback_" + ComposeFullMethodName(method);
            var parentType = method.DeclaringType.NestedTypes.First(m => m.Name == "PrototypeClass");

            TypeReference multicastDeligateType = parentType.Module.Import(typeof(MulticastDelegate));
            TypeReference voidType = parentType.Module.Import(typeof(void));
            TypeReference objectType = parentType.Module.Import(typeof(object));
            TypeReference intPtrType = parentType.Module.Import(typeof(IntPtr));

            TypeDefinition result = new TypeDefinition(null, deligateName,
                TypeAttributes.Sealed | TypeAttributes.NestedPublic | TypeAttributes.RTSpecialName, multicastDeligateType);

            //create constructor
            var constructor = new MethodDefinition(".ctor",
                                                   MethodAttributes.Public | MethodAttributes.CompilerControlled |
                                                   MethodAttributes.RTSpecialName | MethodAttributes.SpecialName |
                                                   MethodAttributes.HideBySig, voidType);
            constructor.Parameters.Add(new ParameterDefinition("object", ParameterAttributes.None, objectType));
            constructor.Parameters.Add(new ParameterDefinition("method", ParameterAttributes.None, intPtrType));
            constructor.IsRuntime = true;
            result.Methods.Add(constructor);


            //create Invoke
            var invoke = new MethodDefinition("Invoke",
                                              MethodAttributes.Public | MethodAttributes.HideBySig |
                                              MethodAttributes.NewSlot | MethodAttributes.Virtual, method.ReturnType);
            invoke.IsRuntime = true;
            if (!method.IsStatic)
            {
                if (method.DeclaringType.HasGenericParameters)
                {

                    ParameterDefinition param = new ParameterDefinition("self", ParameterAttributes.None, method.DeclaringType);
                    param.ParameterType = new GenericInstanceType(param.ParameterType);

                    foreach (var genericParam in method.DeclaringType.GenericParameters)
                        ((GenericInstanceType)(param.ParameterType)).GenericArguments.Add(genericParam);

                    invoke.Parameters.Add(param);
                }
                else
                {
                    invoke.Parameters.Add(new ParameterDefinition("self", ParameterAttributes.None, method.DeclaringType));
                }
            }
            foreach (var param in method.Parameters)
            {
                invoke.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
            }
            result.Methods.Add(invoke);

            //the rest of the process
            result.DeclaringType = parentType;
            parentType.NestedTypes.Add(result);
            MakeGenericType(result);
            return result;
        }

        #region helpers

        private static string ComposeFullMethodName(MethodDefinition method)
        {
            bool includeParamsToName = method.DeclaringType.Methods.Where(m => m.Name == method.Name).Count() > 1;
            var @params = method.Parameters.Select(p =>
            {
                if (p.ParameterType.IsArray)
                {
                    ArrayType array = (ArrayType)p.ParameterType;
                    if (array.Dimensions.Count > 1)
                        return array.ElementType.Name + "Array" + array.Dimensions.Count.ToString();
                    else
                        return array.ElementType.Name + "Array";
                }
                else
                    return p.ParameterType.Name;
            });
            IEnumerable<string> partsOfName = new[] { method.IsConstructor ? "Ctor" : method.Name };
            if (includeParamsToName)
                partsOfName = partsOfName.Concat(@params);

            return string.Join("_", partsOfName.ToArray()).Replace('`', '_');
        }

        private static void MakeGenericType(TypeReference type)
        {
            if (type.DeclaringType.HasGenericParameters)
            {
                foreach (var parentGenericParam in type.DeclaringType.GenericParameters)
                    type.GenericParameters.Add(new GenericParameter(parentGenericParam.Name, type));
            }
        }

        private static void MakeGenericField(FieldDefinition field)
        {
            var prototypeType = field.DeclaringType;
            if (prototypeType.HasGenericParameters)
            {
                var proto = new GenericInstanceType(prototypeType);
                foreach (var param in prototypeType.GenericParameters)
                {
                    proto.GenericArguments.Add(param);
                }
                field.FieldType = proto;
            }
        }

        #endregion

        #region cecil-addons

        public static TypeReference MakeGenericType(this TypeReference self, ICollection<GenericParameter> arguments)
        {
            if (self.GenericParameters.Count != arguments.Count)
                throw new ArgumentException();

            var instance = new GenericInstanceType(self);
            foreach (var argument in arguments)
                instance.GenericArguments.Add(argument);

            return instance;
        }

        public static MethodReference MakeGenericMethod(this MethodReference self, ICollection<GenericParameter> arguments)
        {
            if (self.GenericParameters.Count != arguments.Count)
                throw new ArgumentException();

            var instance = new GenericInstanceMethod(self);
            foreach (var argument in arguments)
                instance.GenericArguments.Add(argument);

            return instance;
        }

        public static MethodReference MakeGeneric(this MethodReference self, ICollection<GenericParameter> arguments)
        {
            var reference = new MethodReference(self.Name, self.ReturnType,
                                                self.DeclaringType.MakeGenericType(arguments));
            reference.HasThis = self.HasThis;
            reference.ExplicitThis = self.ExplicitThis;
            reference.CallingConvention = self.CallingConvention;

            //reference.DeclaringType.GenericParameters.Add(new GenericParameter("T",arguments[0]));

            foreach (var parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

            foreach (var generic_parameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));

            return reference;
        }

        public static FieldReference MakeGeneric(this FieldReference self, ICollection<GenericParameter> arguments)
        {
            return new FieldReference(self.Name, self.FieldType, self.DeclaringType.MakeGenericType(arguments));
        }

        #endregion
    }
}
