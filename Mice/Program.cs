﻿using System;
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

				if (method.HasGenericParameters)
					CreateMethodPrototypesDictionary(method);
				else
					CreateDeligateField(method);

				MethodDefinition newMethod = MoveCodeToImplMethod(method);

				//AddStaticPrototypeCall(method);

				if (!method.IsStatic)
				    if (method.HasGenericParameters)
				        AddGenericInstancePrototypeCall(method, newMethod);
				    else
				        AddInstancePrototypeCall(method);
			}
		}

		private static void CreateMethodPrototypesDictionary(MethodDefinition method)
		{
			var prototypeType = method.DeclaringType.NestedTypes.Single(m => m.Name == "PrototypeClass");
			var delegateType = prototypeType.NestedTypes.Single(m => m.Name == "Callback_" + ComposeFullMethodName(method));
			//TODO: find a way to use delegateType instead of System.Delegate
			var dicType = method.Module.Import(typeof(Dictionary<Type, object>));

			var protoDic = new FieldDefinition('_' + ComposeFullMethodName(method), FieldAttributes.Private, dicType);
			prototypeType.Fields.Add(protoDic);
			protoDic.DeclaringType = prototypeType;

			var Property = new PropertyDefinition(ComposeFullMethodName(method), PropertyAttributes.None, dicType);
			var get = new MethodDefinition("get_" + Property.Name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.CompilerControlled | MethodAttributes.HideBySig, dicType);
			var set = new MethodDefinition("set_" + Property.Name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.CompilerControlled | MethodAttributes.HideBySig, method.Module.Import(typeof(void)));

			//simple IL getter
			get.Body.Variables.Add(new VariableDefinition("[0]", dicType));
			get.Body.GetILProcessor().Emit(OpCodes.Ldarg_0);
			get.Body.GetILProcessor().Emit(OpCodes.Ldfld, protoDic);
			get.Body.GetILProcessor().Emit(OpCodes.Stloc_0);
			get.Body.GetILProcessor().Emit(OpCodes.Ldloc_0);
			get.Body.GetILProcessor().Emit(OpCodes.Ret);

			//simple IL setter
			set.Body.GetILProcessor().Emit(OpCodes.Ldarg_0);
			set.Body.GetILProcessor().Emit(OpCodes.Ldarg_1);
			set.Body.GetILProcessor().Emit(OpCodes.Stfld, protoDic);
			set.Body.GetILProcessor().Emit(OpCodes.Ret);

			Property.GetMethod = get;
			prototypeType.Methods.Add(get);

			set.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, dicType));
			Property.SetMethod = set;
			prototypeType.Methods.Add(set);

			prototypeType.Properties.Add(Property);
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
			if (method.HasGenericParameters)
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

			if (method.ReturnType.IsGenericParameter)
			{
				invoke.GenericParameters.Add(new GenericParameter(method.ReturnType.Name, invoke));
			}

			foreach (var param in method.Parameters)
			{
				if (param.ParameterType.IsGenericParameter && !method.IsConstructor)
				{
					//TODO: remake this
					invoke.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, result.GenericParameters.FirstOrDefault(m => m.Name == param.ParameterType.Name)));
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

			FieldDefinition delegateField;
			delegateField = method.DeclaringType.NestedTypes.Single(m => m.Name == "PrototypeClass").Fields.Single(m => m.Name == ComposeFullMethodName(method));

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

		private static void AddGenericInstancePrototypeCall(MethodDefinition method, MethodDefinition real_method)
		{

			//external types
			var systemTypeClass = method.Module.Import(typeof(Type));
			var objectType = method.Module.Import(typeof(object));

			//external call
			var TypeFromHandle = method.Module.Import(systemTypeClass.Resolve().Methods.Single(m => m.Name == "GetTypeFromHandle"));
			
			//setup Dictionary<Type,object> and it's get accessor 
			var dictType = method.Module.Import(typeof(Dictionary<Type, object>));
			var dictGetMethod = method.Module.Import(dictType.Resolve().Methods.Single(m => m.Name == "get_Item"));
			dictGetMethod.DeclaringType = dictType;

			//setup prototype class and delegate for method
			var protoClassType = method.DeclaringType.NestedTypes.Single(m => m.Name == "PrototypeClass");
			var protoDelegateType = protoClassType.NestedTypes.Single(m => m.Name == "Callback_" + ComposeFullMethodName(method));
			var protoDelegate = new GenericInstanceType(protoDelegateType);

			//setup filds
			var protoField = method.DeclaringType.Fields.Single(m => m.Name == method.DeclaringType.Name.Replace('`', '_') + "Prototype");
			var dictField = protoClassType.Fields.Single(m => m.Name == '_' + ComposeFullMethodName(method));
			
			//setup body variables
			foreach (var param in method.GenericParameters)
				method.Body.Variables.Add(new VariableDefinition(param));
			method.Body.Variables.Add(new VariableDefinition(method.Module.Import(typeof(bool)))); 

			//setup pointers for br* opcodes
			var firstOpcode = method.Body.Instructions.First(); //points to call to default method
			var lastOpcode = method.Body.Instructions[method.Body.Instructions.Count-2]; //points to stloc
			var il = method.Body.GetILProcessor();

			//setup invoke method
			//TODO: remove hack!!
			var protoInvokeDef = protoDelegateType.Methods.Single(m => m.Name == "Invoke");
			var protoInvoke = protoInvokeDef.Instance(protoDelegate);
			protoInvoke.ReturnType = protoInvoke.Parameters[1].ParameterType;

			foreach (var param in method.DeclaringType.GenericParameters.Concat(method.GenericParameters))
				protoDelegate.GenericArguments.Add(param);

			var instructions = new Instruction[]
			{
			//TODO: do we need every stloc-ldloc pair?
			Instruction.Create(OpCodes.Nop),
			Instruction.Create(OpCodes.Ldarg_0),
			Instruction.Create(OpCodes.Ldflda, protoField.Instance()),  // valuetype Cheese.GenericStorage`1/Test<!0> class Cheese.GenericStorage`1<!T>::testSample
			Instruction.Create(OpCodes.Ldfld, dictField),   // class [mscorlib]System.Collections.Generic.Dictionary`2<class [mscorlib]System.Type, object> valuetype Cheese.GenericStorage`1/Test<!T>::Dict
			Instruction.Create(OpCodes.Ldtoken, method.GenericParameters[0]), // !!L
			Instruction.Create(OpCodes.Call, TypeFromHandle),    // class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
			Instruction.Create(OpCodes.Callvirt, dictGetMethod),// instance !1 class [mscorlib]System.Collections.Generic.Dictionary`2<class [mscorlib]System.Type, object>::get_Item(!0)
			Instruction.Create(OpCodes.Ldnull),
			Instruction.Create(OpCodes.Ceq), 
			Instruction.Create(OpCodes.Stloc_1),
			Instruction.Create(OpCodes.Ldloc_1),
			Instruction.Create(OpCodes.Brtrue_S, firstOpcode), // IL_004b

			Instruction.Create(OpCodes.Nop), 
			Instruction.Create(OpCodes.Ldarg_0),
			Instruction.Create(OpCodes.Ldflda, protoField.Instance()), // valuetype Cheese.GenericStorage`1/Test<!0> class Cheese.GenericStorage`1<!T>::testSample
			Instruction.Create(OpCodes.Ldfld, dictField), // class [mscorlib]System.Collections.Generic.Dictionary`2<class [mscorlib]System.Type, object> valuetype Cheese.GenericStorage`1/Test<!T>::Dict
			Instruction.Create(OpCodes.Ldtoken, method.GenericParameters[0]), // !!L
			Instruction.Create(OpCodes.Call, TypeFromHandle), // class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
			Instruction.Create(OpCodes.Callvirt, dictGetMethod), // instance !1 class [mscorlib]System.Collections.Generic.Dictionary`2<class [mscorlib]System.Type, object>::get_Item(!0)
			Instruction.Create(OpCodes.Castclass, protoDelegate), // class Cheese.GenericStorage`1/Test/Maker`1<!T, !!L>
			Instruction.Create(OpCodes.Ldarg_0),
			Instruction.Create(OpCodes.Ldarg_1), 
			Instruction.Create(OpCodes.Callvirt, protoInvoke), // instance !1 class Cheese.GenericStorage`1/Test/Maker`1<!T, !!L>::Invoke(!1)
			Instruction.Create(OpCodes.Stloc_0),
			Instruction.Create(OpCodes.Br_S, lastOpcode), // IL_0055
			};

			foreach(Instruction inst in instructions)
				il.InsertBefore(firstOpcode,inst);
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
			string name = "x"; //real implementation prefix

			if (method.IsConstructor)
				name += "Ctor";
			else if (method.IsSetter && method.Name.StartsWith("set_"))
				name = "set_" + name + method.Name.Substring(4);
			else if (method.IsGetter && method.Name.StartsWith("get_"))
				name = "get_" + name + method.Name.Substring(4);
			else
				name += method.Name;

			MethodDefinition result = new MethodDefinition(name, method.Attributes, method.ReturnType);

			result.SemanticsAttributes = method.SemanticsAttributes;

			result.IsRuntimeSpecialName = false;
			result.IsVirtual = false;
			if (method.IsConstructor)
				result.IsSpecialName = false;

			foreach (var genericParam in method.GenericParameters)
				result.GenericParameters.Add(new GenericParameter(genericParam.Name, result));

			foreach (var param in method.Parameters)
				result.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));

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

			//if (method.HasGenericParameters)
			//{
			//    //for some reason we need to import variables correctly
			//    result.Body.Variables.Clear();
			//    foreach (var genericParam in method.GenericParameters)
			//        result.Body.Variables.Add(new VariableDefinition(genericParam.Name, genericParam));
			//}

			for (int i = 0; i < allParamsCount; i++)
				il.Emit(OpCodes.Ldarg, i);

			il.Emit(OpCodes.Call, result.Instance());
			il.Emit(OpCodes.Stloc_0);
			il.Emit(OpCodes.Ldloc_0);
			il.Emit(OpCodes.Ret);
			return result;
		}

		#region helpers

		private static string ComposeFullMethodName(MethodDefinition method)
		{
			StringBuilder FullName = new StringBuilder();
			bool includeParamsToName = method.DeclaringType.Methods.Where(m => m.Name == method.Name).Count() > 1;

			FullName.Append(method.IsConstructor ? "Ctor" : method.Name);

			foreach (var p in method.Parameters)
			{
				FullName.Append('_');
				if (p.ParameterType.IsArray)
				{
					ArrayType array = (ArrayType)p.ParameterType;
					FullName.Append(array.ElementType.Name + "Array");
					if (array.Dimensions.Count > 1)
						FullName.Append(array.Dimensions.Count.ToString());
				}
				else
					FullName.Append(p.ParameterType.Name);
			}

			if (method.HasGenericParameters)
				FullName.Append('`' + method.GenericParameters.Count.ToString());

			return FullName.ToString();
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

		private static void ImportGenericParams(this MethodReference self, IGenericParameterProvider source)
		{
			foreach(var genericParam in source.GenericParameters)
			{
				self.GenericParameters.Add(genericParam);
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

		public static MethodReference Instance(this MethodDefinition self, TypeReference DeclaringType = null)
		{
			TypeReference declaringType;
			if (DeclaringType == null)
				declaringType = self.DeclaringType.Instance();
			else
				declaringType = DeclaringType;

			var arguments = self.DeclaringType.GenericParameters;
			var reference = new MethodReference(self.Name, self.ReturnType, declaringType);
			reference.HasThis = self.HasThis;
			reference.ExplicitThis = self.ExplicitThis;
			reference.CallingConvention = self.CallingConvention;

			foreach (var parameter in self.Parameters)
				reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

			foreach (var generic_parameter in self.GenericParameters)
				reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));

			if (self.HasGenericParameters && !(self.IsGetter) && !(self.IsSetter) && DeclaringType == null)
			{
				reference = new GenericInstanceMethod(reference);
				foreach (var genericParam in self.GenericParameters)
					((GenericInstanceMethod)(reference)).GenericArguments.Add(genericParam);
			}

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