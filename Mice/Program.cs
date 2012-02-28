using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using StrongNameKeyPair = System.Reflection.StrongNameKeyPair;
using System.IO;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using PropertyAttributes = Mono.Cecil.PropertyAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

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

			if (!type.IsAbstract)
			{
				var privateDefaultCtor =
					type.Methods.SingleOrDefault(m => m.IsConstructor && !m.HasParameters && !m.IsPublic && !m.IsStatic);

				var publicDefaultCtor =
						type.Methods.SingleOrDefault(m => m.IsConstructor && !m.HasParameters && m.IsPublic && !m.IsStatic);

				if (privateDefaultCtor != null)
				{
					//TODO:gona make it public. later
					CreateDeligateType(privateDefaultCtor);
					CreateDeligateField(privateDefaultCtor);

					AddPrototypeCalls(privateDefaultCtor, MoveCodeToImplMethod(privateDefaultCtor));
					CreateCallToPrivateCtor(privateDefaultCtor, prototypeType);
				}
				else if (publicDefaultCtor == null) //there is not default ctor, neither private nor public
				{
					publicDefaultCtor = CreateDefaultCtor(type);
					//that is here only for compability with old tests
					//because now we create bulic constructor instead of private one
					CreateCallToPrivateCtor(publicDefaultCtor, prototypeType);

				}
			}

			//create delegate types & fields, patch methods to call delegates
			var processingMethods = type.Methods.Where(IsMethodToBeProcessed).ToArray();
			foreach (var method in processingMethods)
			{
				CreateDeligateType(method);

				if (method.HasGenericParameters)
				{
					CreateMethodPrototypesDictionary(method);
					AddGenericPrototypeCalls(method, MoveCodeToImplMethod(method));
				}
				else
				{
					CreateDeligateField(method);
					AddPrototypeCalls(method, MoveCodeToImplMethod(method));
				}
			}
		}

		private static void CreateMethodPrototypesDictionary(MethodDefinition method)
		{
			var prototypeType = method.DeclaringType.NestedTypes.Single(m => m.Name == "PrototypeClass");
			var delegateType = prototypeType.NestedTypes.Single(m => m.Name == "Callback_" + ComposeFullMethodName(method));
			var dicType = method.Module.Import(typeof(Dictionary<Type, object>));
			var dicConst = method.Module.Import(typeof(Dictionary<Type, object>).GetConstructor(Type.EmptyTypes));


			var protoDic = new FieldDefinition('_'+ComposeFullMethodName(method), FieldAttributes.Private, dicType);
			protoDic.DeclaringType = prototypeType;
			prototypeType.Fields.Add(protoDic);
			
			//TODO: initialize it somewhere

			var Property = new PropertyDefinition(ComposeFullMethodName(method), PropertyAttributes.None, dicType);
			var get = new MethodDefinition("get_" + Property.Name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.CompilerControlled | MethodAttributes.HideBySig, dicType);
			var set = new MethodDefinition("set_" + Property.Name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.CompilerControlled | MethodAttributes.HideBySig, method.Module.Import(typeof(void)));

			//IL getter
			get.Body.Variables.Add(new VariableDefinition(dicType));
			get.Body.Variables.Add(new VariableDefinition(method.Module.Import(typeof(bool))));

			get.Body.InitLocals = true;
			set.Body.InitLocals = true;

			ICollection<Instruction> jmpReplacements = new Collection<Instruction>();

			var il = get.Body.GetILProcessor();

			//check for dictionary existance
			il.Emit(OpCodes.Nop);
			il.Emit(OpCodes.Ldarg_0); 
			il.Emit(OpCodes.Ldfld, protoDic.Instance()); // class [mscorlib]System.Collections.Generic.Dictionary`2<class [mscorlib]System.Type, object> valuetype Cheese.GenericStorage`1/Test<!T>::Dict
			il.Emit(OpCodes.Ldnull); 
			il.Emit(OpCodes.Ceq); 
			il.Emit(OpCodes.Ldc_I4_0); 
			il.Emit(OpCodes.Ceq); 
			il.Emit(OpCodes.Stloc_1);
			il.Emit(OpCodes.Ldloc_1);
			il.Emit(OpCodes.Brtrue_S, il.Body.Instructions.Last()); // IL_001e
			jmpReplacements.Add(il.Body.Instructions.Last());

			//create dictionary
			il.Emit(OpCodes.Nop);
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Newobj,dicConst); // instance void class [mscorlib]System.Collections.Generic.Dictionary`2<class [mscorlib]System.Type, object>::.ctor()
			il.Emit(OpCodes.Stfld, protoDic.Instance());  // class [mscorlib]System.Collections.Generic.Dictionary`2<class [mscorlib]System.Type, object> valuetype Cheese.GenericStorage`1/Test<!T>::Dict

			il.Emit(OpCodes.Nop);
			foreach (var jmpReplacement in jmpReplacements) jmpReplacement.Operand = il.Body.Instructions.Last();

			//return dictionary
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldfld, protoDic.Instance()); // class [mscorlib]System.Collections.Generic.Dictionary`2<class [mscorlib]System.Type, object> valuetype Cheese.GenericStorage`1/Test<!T>::Dict
			il.Emit(OpCodes.Stloc_0);
			il.Emit(OpCodes.Ldloc_0); 
			il.Emit(OpCodes.Ret); 
			
			//IL setter
			set.Body.GetILProcessor().Emit(OpCodes.Ldarg_0);
			set.Body.GetILProcessor().Emit(OpCodes.Ldarg_1);
			set.Body.GetILProcessor().Emit(OpCodes.Stfld, protoDic.Instance());
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

			//not a good solution, but it don't work any other way
			var ret = method.ReturnType;
			if (method.ReturnType.IsGenericParameter && method.GenericParameters.SingleOrDefault(m=>m.Name==method.ReturnType.Name)!=null)
				ret = result.GenericParameters.SingleOrDefault(m => m.Name == method.ReturnType.Name);
			

			//create Invoke
			var invoke = new MethodDefinition("Invoke",
											  MethodAttributes.Public | MethodAttributes.HideBySig |
											  MethodAttributes.NewSlot | MethodAttributes.Virtual, ret);

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

		private static void AddPrototypeCalls(MethodDefinition method, MethodDefinition real_method)
		{
			var staticPrototypeField = method.DeclaringType.Fields.Single(m => m.Name == "StaticPrototype");
			var dynamicPrototypeField = method.DeclaringType.Fields.SingleOrDefault(m => m.Name == method.DeclaringType.Name.Replace('`', '_') + "Prototype");
			var delegateField = method.DeclaringType.NestedTypes.Single(m => m.Name == "PrototypeClass").Fields.Single(m => m.Name == ComposeFullMethodName(method));

			var il = method.Body.GetILProcessor();
			List<Instruction> jmpReplacements = new List<Instruction>();
			TypeDefinition delegateType = delegateField.FieldType.Resolve();
			var invokeMethod = delegateType.Methods.Single(m => m.Name == "Invoke");
			int allParamsCount = method.Parameters.Count + (method.IsStatic ? 0 : 1); //all params and maybe this


			if (!method.IsStatic)
			{
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldflda, dynamicPrototypeField.Instance());
				il.Emit(OpCodes.Ldfld, delegateField.Instance());
				il.Emit(OpCodes.Brfalse, il.Body.Instructions.First());
				jmpReplacements.Add(il.Body.Instructions.Last()); //addres will be replaced

				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldflda, dynamicPrototypeField.Instance());
				il.Emit(OpCodes.Ldfld, delegateField.Instance());
				for (int i = 0; i < allParamsCount; i++) il.Emit(OpCodes.Ldarg, i);
				il.Emit(OpCodes.Callvirt, invokeMethod.Instance());
				il.Emit(OpCodes.Ret);
			}

			il.Emit(OpCodes.Nop);
			foreach (var jmpReplacement in jmpReplacements)
				jmpReplacement.Operand = il.Body.Instructions.Last();
			jmpReplacements.Clear();

			il.Emit(OpCodes.Ldsflda, staticPrototypeField.Instance());
			il.Emit(OpCodes.Ldfld, delegateField.Instance());
			il.Emit(OpCodes.Brfalse, il.Body.Instructions.First()); //addres will be replaced
			jmpReplacements.Add(il.Body.Instructions.Last());

			il.Emit(OpCodes.Ldsflda, staticPrototypeField.Instance());
			il.Emit(OpCodes.Ldfld, delegateField.Instance());
			for (int i = 0; i < allParamsCount; i++) il.Emit(OpCodes.Ldarg, i);
			il.Emit(OpCodes.Callvirt, invokeMethod.Instance());
			il.Emit(OpCodes.Ret);

			il.Emit(OpCodes.Nop);
			foreach (var jmpReplacement in jmpReplacements)
				jmpReplacement.Operand = il.Body.Instructions.Last();

			for (int i = 0; i < allParamsCount; i++) il.Emit(OpCodes.Ldarg, i);
			il.Emit(OpCodes.Call, real_method.Instance());
			il.Emit(OpCodes.Ret);
		}

		private static void AddGenericPrototypeCalls(MethodDefinition method, MethodDefinition real_method)
		{
			//external types
			var systemTypeClass = method.Module.Import(typeof(Type));
			Type[] systemFuncs = {typeof(Func<>),typeof(Func<,>),typeof(Func<,,>),typeof(Func<,,,>),typeof(Func<,,,,>)};
			var systemFuncClass = method.Module.Import(systemFuncs[method.GenericParameters.Count-1]);

			//external call
			var TypeFromHandle = method.Module.Import(systemTypeClass.Resolve().Methods.Single(m => m.Name == "GetTypeFromHandle"));

			//setup Dictionary<Type,object> and it's ContainsKey method
			var dictType = method.Module.Import(typeof(Dictionary<Type, object>));
			var dictContainsKeyMethod = method.Module.Import(dictType.Resolve().Methods.Single(m => m.Name == "ContainsKey"));
			var dictGetItemMethod = method.Module.Import(dictType.Resolve().Methods.Single(m => m.Name == "get_Item"));
			dictContainsKeyMethod.DeclaringType = dictType;
			dictGetItemMethod.DeclaringType = dictType;

			//setup prototype class and delegate for method
			var protoClassType = method.DeclaringType.NestedTypes.Single(m => m.Name == "PrototypeClass");
			var protoDelegateType = protoClassType.NestedTypes.Single(m => m.Name == "Callback_" + ComposeFullMethodName(method));

			//setup fields
			var protoField = method.DeclaringType.Fields.Single(m => m.Name == method.DeclaringType.Name.Replace('`', '_') + "Prototype");
			var staticProtoField = method.DeclaringType.Fields.Single(m => m.Name == "StaticPrototype");
			
			var dictField = protoClassType.Fields.Single(m => m.Name == '_'+ComposeFullMethodName(method));
			var dictProperty = protoClassType.Properties.Single(m => m.Name == ComposeFullMethodName(method));

			//setup delegate proto
			var protoDelegate = new GenericInstanceType(protoDelegateType);
			foreach (var param in method.DeclaringType.GenericParameters.Concat(method.GenericParameters))
				protoDelegate.GenericArguments.Add(param);

			//setup invoke method
			var protoInvoke = protoDelegateType.Methods.Single(m => m.Name == "Invoke");

			var il = method.Body.GetILProcessor();
			//thus is a place to store instructions, which operands needs to b
			List<Instruction> jmpReplacements = new List<Instruction>();

			var allParamsCount = method.Parameters.Count + (method.IsStatic ? 0 : 1);

			//setup body variables
			method.Body.InitLocals = true;
			method.Body.Variables.Add(new VariableDefinition(systemTypeClass)); //key
			method.Body.Variables.Add(new VariableDefinition(method.ReturnType)); //data to return
			method.Body.Variables.Add(new VariableDefinition(method.Module.Import(typeof(bool)))); //for evaluation of conditions

			il.Emit(OpCodes.Nop);

			//making key variable
			systemFuncClass = new GenericInstanceType(systemFuncClass);
			foreach (var param in method.GenericParameters)
				((GenericInstanceType)systemFuncClass).GenericArguments.Add(param);

			il.Emit(OpCodes.Ldtoken, systemFuncClass);
			il.Emit(OpCodes.Call,TypeFromHandle);
			il.Emit(OpCodes.Stloc_0);
			
			if (!method.IsStatic)
			{
				//finding key in dictionary
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldflda, protoField.Instance());	
				il.Emit(OpCodes.Call, dictProperty.GetMethod.Instance());
				il.Emit(OpCodes.Ldloc_0);
				il.Emit(OpCodes.Callvirt, dictContainsKeyMethod);
				il.Emit(OpCodes.Ldc_I4_0);
				il.Emit(OpCodes.Ceq);
				il.Emit(OpCodes.Stloc_2);
				il.Emit(OpCodes.Ldloc_2);
				il.Emit(OpCodes.Brtrue_S, il.Body.Instructions.Last()); //will be replaced
				jmpReplacements.Add(il.Body.Instructions.Last()); //=>to static prototype

				//if key is found - call proto function
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldflda, protoField.Instance()); // valuetype Cheese.GenericStorage`1/Test<!0> class Cheese.GenericStorage`1<!T>::testSample
				il.Emit(OpCodes.Call, dictProperty.GetMethod.Instance());
				il.Emit(OpCodes.Ldloc_0);
				il.Emit(OpCodes.Callvirt, dictGetItemMethod); // instance !1 class [mscorlib]System.Collections.Generic.Dictionary`2<class [mscorlib]System.Type[], object>::get_Item(!0)
				il.Emit(OpCodes.Castclass, protoDelegate); // class Cheese.GenericStorage`1/Test/Maker`1<!T, !!L>
				for (int i = 0; i < allParamsCount; i++)
					il.Emit(OpCodes.Ldarg, i);

				il.Emit(OpCodes.Callvirt, protoInvoke.Instance(method.DeclaringType.GenericParameters, method.GenericParameters)); // instance !1 class Cheese.GenericStorage`1/Test/Maker`1<!T, !!L>::Invoke(class Cheese.GenericStorage`1<!0>, !1)
				il.Emit(OpCodes.Stloc_1); //
				il.Emit(OpCodes.Br_S, il.Body.Instructions.Last()); //will be replaced

				il.Emit(OpCodes.Nop);
				jmpReplacements[0].Operand = il.Body.Instructions.Last();
				jmpReplacements.Clear();
				jmpReplacements.Add(il.Body.Instructions[il.Body.Instructions.Count - 2]); //=>to ret
			}

			//finding key in static prototype dictionary
			il.Emit(OpCodes.Ldsflda, staticProtoField.Instance());
			il.Emit(OpCodes.Call, dictProperty.GetMethod.Instance());
			il.Emit(OpCodes.Ldloc_0);
			il.Emit(OpCodes.Callvirt, dictContainsKeyMethod);
			il.Emit(OpCodes.Ldc_I4_0);
			il.Emit(OpCodes.Ceq);
			il.Emit(OpCodes.Stloc_2);
			il.Emit(OpCodes.Ldloc_2);
			il.Emit(OpCodes.Brtrue_S, il.Body.Instructions.Last()); //will be replaced
			jmpReplacements.Add(il.Body.Instructions.Last()); //=>to call to defualt x-method

			//if key is found - call proto function
			il.Emit(OpCodes.Nop);
			il.Emit(OpCodes.Ldsflda, staticProtoField.Instance()); // valuetype Cheese.GenericStorage`1/Test<!0> class Cheese.GenericStorage`1<!T>::testSample
			il.Emit(OpCodes.Call, dictProperty.GetMethod.Instance());
			il.Emit(OpCodes.Ldloc_0);
			il.Emit(OpCodes.Callvirt, dictGetItemMethod); // instance !1 class [mscorlib]System.Collections.Generic.Dictionary`2<class [mscorlib]System.Type[], object>::get_Item(!0)
			il.Emit(OpCodes.Castclass, protoDelegate); // class Cheese.GenericStorage`1/Test/Maker`1<!T, !!L>
			for (int i = 0; i < allParamsCount; i++)
				il.Emit(OpCodes.Ldarg, i);

			il.Emit(OpCodes.Callvirt, protoInvoke.Instance(method.DeclaringType.GenericParameters, method.GenericParameters)); // instance !1 class Cheese.GenericStorage`1/Test/Maker`1<!T, !!L>::Invoke(class Cheese.GenericStorage`1<!0>, !1)
			il.Emit(OpCodes.Stloc_1); //
			il.Emit(OpCodes.Br_S, il.Body.Instructions.Last()); // IL_0067

			il.Emit(OpCodes.Nop);
			jmpReplacements.Last().Operand = il.Body.Instructions.Last();
			jmpReplacements.Remove(jmpReplacements.Last());
			jmpReplacements.Add(il.Body.Instructions[il.Body.Instructions.Count - 2]);

			//call x-method by default
			for (int i = 0; i < allParamsCount; i++)
				il.Emit(OpCodes.Ldarg, i);
			il.Emit(OpCodes.Call, real_method.Instance());
			il.Emit(OpCodes.Stloc_1);
			il.Emit(OpCodes.Ldloc_1);

			foreach (var jmpReplacement in jmpReplacements)
				jmpReplacement.Operand = il.Body.Instructions.Last();

			il.Emit(OpCodes.Ret);

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
			return result;
		}

		#region helpers

		private static string ComposeFullMethodName(MethodDefinition method)
		{
			StringBuilder FullName = new StringBuilder();
			bool includeParamsToName = method.DeclaringType.Methods.Where(m => m.Name == method.Name).Count() > 1;


			FullName.Append(method.IsConstructor ? "Ctor" : method.Name);
			if (includeParamsToName)
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
				FullName.Append('_' + method.GenericParameters.Count.ToString());

			return FullName.ToString();
		}

		private static void ImportGenericParams(this TypeDefinition type, IGenericParameterProvider source)
		{
			if (source.HasGenericParameters)
			{
				foreach (var parentGenericParam in source.GenericParameters)
					type.GenericParameters.Add(new GenericParameter(parentGenericParam.Name, type));
			}
		}

		private static void ImportGenericParams(this MethodReference self, IGenericParameterProvider source)
		{
			foreach (var genericParam in source.GenericParameters)
			{
				self.GenericParameters.Add(genericParam);
			}
		}

		//Instance methods are used to convert *Definition -> *Reference

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

		//that one is for normal use - like calling class members, etc 
		public static MethodReference Instance(this MethodDefinition self)
		{
			var declaringType = self.DeclaringType.Instance();

			var reference = new MethodReference(self.Name, self.ReturnType, declaringType);
			reference.HasThis = self.HasThis;
			reference.ExplicitThis = self.ExplicitThis;
			reference.CallingConvention = self.CallingConvention;

			foreach (var parameter in self.Parameters)
				reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

			foreach (var generic_parameter in self.GenericParameters)
				reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));



			if (self.HasGenericParameters && !self.IsGetter && !self.IsSetter)
			{
				reference = new GenericInstanceMethod(reference);
				foreach (var genericParam in self.GenericParameters)
					if (self.DeclaringType.GenericParameters.SingleOrDefault(m => m.Name == genericParam.Name) == null)
						((GenericInstanceMethod)(reference)).GenericArguments.Add(genericParam);
			}

			return reference;
		}

		//that one is for virtual calls. Generic Arguments are set manually.
		public static MethodReference Instance(this MethodDefinition self, params ICollection<GenericParameter>[] genericArgs)
		{
			var declaringType = new GenericInstanceType(self.DeclaringType);
			foreach (var param in genericArgs)
				foreach (var genericParameter in param)
					declaringType.GenericArguments.Add(genericParameter);

			//TODO: WARNING! that may be wrong! need testing...
			//var ret = self.GenericParameters.Single(m => m.Name == self.ReturnType.Name);//new TypeReference(null, self.ReturnType.Name, self.Module, self.ReturnType.Scope);
			//var ret = new TypeDefinition(null, self.ReturnType.Name, TypeAttributes.Public,null);

			var reference = new MethodReference(self.Name, self.ReturnType, declaringType);
			reference.HasThis = self.HasThis;
			reference.ExplicitThis = self.ExplicitThis;
			reference.CallingConvention = self.CallingConvention;

			foreach (var parameter in self.Parameters)
				reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

			return reference;
		}

		#endregion
	}
}