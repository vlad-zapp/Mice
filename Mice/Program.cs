using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StrongNameKeyPair = System.Reflection.StrongNameKeyPair;
using System.IO;

namespace Mice
{
    static class Program
    {
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
                !m.IsAbstract &&
                !(m.IsStatic && m.IsConstructor);
        }

        private static void ProcessType(TypeDefinition type)
        {
            TypeDefinition prototypeType = CreatePrototypeType(type);

            FieldDefinition prototypeField = new FieldDefinition(type.Name.Replace('`', '_') + "Prototype", FieldAttributes.Public, prototypeType.Instance());
            type.Fields.Add(prototypeField);

            FieldDefinition staticPrototypeField = new FieldDefinition("StaticPrototype", FieldAttributes.Public | FieldAttributes.Static, prototypeType.Instance());
            type.Fields.Add(staticPrototypeField);
            
            //After using of Mice there always should be a way to create an instance of public class
            //Here we create methods that can call parameterless ctor, evern if there is no parameterless ctor :)
            
            //temprorary disabled
            //if (!type.IsAbstract)
            //{
            //    var privateDefaultCtor =
            //        type.Methods.SingleOrDefault(m => m.IsConstructor && !m.HasParameters && !m.IsPublic && !m.IsStatic);

            //    var publicDefaultCtor =
            //            type.Methods.SingleOrDefault(m => m.IsConstructor && !m.HasParameters && m.IsPublic && !m.IsStatic);

            //    if (privateDefaultCtor != null)
            //    {
            //        //TODO:gona make it public. later
            //        CreateDeligateType(privateDefaultCtor);
            //        CreateDeligateField(privateDefaultCtor);

            //        MethodDefinition newMethod = MoveCodeToImplMethod(privateDefaultCtor);

            //        AddStaticPrototypeCall(privateDefaultCtor);
            //        CreateCallToPrivateCtor(privateDefaultCtor, prototypeType);
            //    }
            //    else if (publicDefaultCtor == null) //there is not default ctor, neither private nor public
            //    {
            //        publicDefaultCtor = CreateDefaultCtor(type);
            //        //that is here only for compability with old tests
            //        //because now we create bulic constructor instead of private one
            //        CreateCallToPrivateCtor(publicDefaultCtor, prototypeType);

            //    }
            //}
            
            //create delegate types & fields, patch methods to call delegates
            var processingMethods = type.Methods.Where(IsMethodToBeProcessed).ToArray();
            foreach (var method in processingMethods)
            {
                CreateDeligateType(method);

                //the end for methods with generics for now
                if (method.HasGenericParameters) return;
                
                CreateDeligateField(method);



                MethodDefinition newMethod = MoveCodeToImplMethod(method);

                AddStaticPrototypeCall(method);

                if (!method.IsStatic)
                {
                    AddInstancePrototypeCall(method);
                }
            }
        }

        private static TypeDefinition CreatePrototypeType(TypeDefinition type)
        {
            TypeDefinition result = new TypeDefinition(null, "PrototypeClass",
                TypeAttributes.Sealed | TypeAttributes.NestedPublic | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout,
                type.Module.Import(typeof(ValueType)));

            result.ImportGenericParams(type);
            type.NestedTypes.Add(result);
            result.DeclaringType = type;
            return result;
        }

        private static MethodDefinition CreateDefaultCtor(TypeDefinition type)
        {
            //create constructor
            var constructor = new MethodDefinition(".ctor",
                MethodAttributes.Public | MethodAttributes.CompilerControlled |
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
            il.Emit(OpCodes.Newobj, defCtor.Instance());
            il.Emit(OpCodes.Ret);

            prototypeType.Methods.Add(result);

            return result;
        }

        private static FieldDefinition CreateDeligateField(MethodDefinition method)
        {
            var fieldName = ComposeFullMethodName(method);
            var parentClass = method.DeclaringType.NestedTypes.Single(m => m.Name == "PrototypeClass");
            var delegateType = parentClass.NestedTypes.Single(m => m.Name == "Callback_" + fieldName);

            FieldDefinition field = new FieldDefinition(fieldName, FieldAttributes.Public, delegateType.Instance());
            parentClass.Fields.Add(field);

            return field;
        }

        private static TypeDefinition CreateDeligateType(MethodDefinition method)
        {
            string deligateName = "Callback_" + ComposeFullMethodName(method);
            var parentType = method.DeclaringType.NestedTypes.Single(m => m.Name == "PrototypeClass");

            TypeReference multicastDeligateType = parentType.Module.Import(typeof(MulticastDelegate));
            TypeReference voidType = parentType.Module.Import(typeof(void));
            TypeReference objectType = parentType.Module.Import(typeof(object));
            TypeReference intPtrType = parentType.Module.Import(typeof(IntPtr));

            TypeDefinition result = new TypeDefinition(null, deligateName,
                TypeAttributes.Sealed | TypeAttributes.NestedPublic | TypeAttributes.RTSpecialName, multicastDeligateType);

            result.ImportGenericParams(parentType);
            if(method.HasGenericParameters)
                result.ImportGenericParams(method);

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
                invoke.Parameters.Add(new ParameterDefinition("self", ParameterAttributes.None, method.DeclaringType.Instance()));
            }

            foreach (var param in method.Parameters)
            {
                if (param.ParameterType.IsGenericParameter && !method.IsConstructor)
                {
                    //TODO: remake this
                    invoke.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, result.GenericParameters.FirstOrDefault(m=>m.Name==param.ParameterType.Name)));
                }
                else
                    invoke.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
            }

            result.Methods.Add(invoke);
           
            //the rest of the process
            result.DeclaringType = parentType;
            parentType.NestedTypes.Add(result);
            return result;
        }

        private static void AddStaticPrototypeCall(MethodDefinition method)
        {
            var prototypeField =
                method.DeclaringType.Fields.Single(m => m.Name == "StaticPrototype");
            var delegateField =
                method.DeclaringType.NestedTypes.Single(m => m.Name == "PrototypeClass").
                Fields.Single(m => m.Name == ComposeFullMethodName(method));

            var firstOpcode = method.Body.Instructions.First();
            var il = method.Body.GetILProcessor();

            TypeDefinition delegateType = delegateField.FieldType.Resolve();
            var invokeMethod = delegateType.Methods.Single(m => m.Name == "Invoke");
            int allParamsCount = method.Parameters.Count + (method.IsStatic ? 0 : 1); //all params and maybe this

            var instructions = new[]
			{
				il.Create(OpCodes.Ldsflda, prototypeField.Instance()),
				il.Create(OpCodes.Ldfld, delegateField.Instance()),
				il.Create(OpCodes.Brfalse, firstOpcode),

				il.Create(OpCodes.Ldsflda, prototypeField.Instance()),
				il.Create(OpCodes.Ldfld, delegateField.Instance()),
			}.Concat(
                Enumerable.Range(0, allParamsCount).Select(i => il.Create(OpCodes.Ldarg, i))
            ).Concat(new[]
			{
				il.Create(OpCodes.Callvirt, invokeMethod.Instance()),
				il.Create(OpCodes.Ret),
			});

            foreach (var instruction in instructions)
                il.InsertBefore(firstOpcode, instruction);
        }

        private static void AddInstancePrototypeCall(MethodDefinition method)
        {
            var parentClass = method.DeclaringType.NestedTypes.Single(m => m.Name == "PrototypeClass");
            var prototypeField = method.DeclaringType.Fields.Single(m => m.Name == method.DeclaringType.Name.Replace('`', '_') + "Prototype");
            var delegateField = parentClass.Fields.Single(m => m.Name == ComposeFullMethodName(method));

            var firstOpcode = method.Body.Instructions.First();
            var il = method.Body.GetILProcessor();

            TypeDefinition delegateType = delegateField.FieldType.Resolve();
            var invokeMethod = delegateType.Methods.Single(m => m.Name == "Invoke");
            int allParamsCount = method.Parameters.Count + 1; //all params and this

            var instructions = new[]
			{
				il.Create(OpCodes.Ldarg_0),
				il.Create(OpCodes.Ldflda, prototypeField.Instance()),
				il.Create(OpCodes.Ldfld, delegateField.Instance()),
				il.Create(OpCodes.Brfalse, firstOpcode),

				il.Create(OpCodes.Ldarg_0),
				il.Create(OpCodes.Ldflda, prototypeField.Instance()),
				il.Create(OpCodes.Ldfld, delegateField.Instance()),
			}.Concat(
                Enumerable.Range(0, allParamsCount).Select(i => il.Create(OpCodes.Ldarg, i))
            ).Concat(new[]
			{
				il.Create(OpCodes.Callvirt, invokeMethod.Instance()),
				il.Create(OpCodes.Ret),
			});

            foreach (var instruction in instructions)
                il.InsertBefore(firstOpcode, instruction);
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
                var property = method.DeclaringType.Properties.SingleOrDefault(p => p.Name == propertyName);
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

            if (result.DeclaringType.HasGenericParameters)
            {
                il.Emit(OpCodes.Call, result.Instance());
            }
            else
            {
                il.Emit(OpCodes.Call, result);
            }

            il.Emit(OpCodes.Ret);
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

        private static void ImportGenericParams(this TypeDefinition type, TypeReference source)
        {
            if (source.HasGenericParameters)
            {
                foreach (var parentGenericParam in source.GenericParameters)
                    type.GenericParameters.Add(new GenericParameter(parentGenericParam.Name, type));
            }
        }

        private static void ImportGenericParams(this TypeDefinition type, MethodReference source)
        {
            if (source.HasGenericParameters)
            {
                foreach (var parentGenericParam in source.GenericParameters)
                    type.GenericParameters.Add(new GenericParameter(parentGenericParam.Name, type));
            }
        }

        //Instance methods are used to convert *Definition -> *Reference

        private static TypeReference Instance(this TypeDefinition self)
        {
            if (self.HasGenericParameters)
            {
                var instance = new GenericInstanceType(self);
                foreach (var argument in self.GenericParameters)
                    instance.GenericArguments.Add(argument);
                return instance;
            }
            //implicit convertion TypeDefinition->TypeReference
            return self;
        }

        public static MethodReference Instance(this MethodDefinition self)
        {
            var arguments = self.DeclaringType.GenericParameters;
            var reference = new MethodReference(self.Name, self.ReturnType,
                                                self.DeclaringType.Instance());
            reference.HasThis = self.HasThis;
            reference.ExplicitThis = self.ExplicitThis;
            reference.CallingConvention = self.CallingConvention;

            foreach (var parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

            foreach (var generic_parameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));

            return reference;
        }

        public static FieldReference Instance(this FieldDefinition self)
        {
            FieldReference result = new FieldReference(self.Name, self.FieldType);
            result.DeclaringType = self.DeclaringType.Instance();

            if (self.DeclaringType.HasGenericParameters)
            {
                //((GenericInstanceType) self.FieldType).GenericArguments.Add(self.DeclaringType.GenericParameters[0]);
            }
            return result;
        }

        #endregion
    }
}