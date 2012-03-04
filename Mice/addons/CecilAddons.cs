using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mice.Addons
{
	public static class extras
	{
		public static void ImportGenericParams(this TypeDefinition type, IGenericParameterProvider source)
		{
			if (source.HasGenericParameters)
			{
				foreach (var parentGenericParam in source.GenericParameters)
					type.GenericParameters.Add(new GenericParameter(parentGenericParam.Name, type));
			}
		}

		public static void ImportGenericParams(this MethodReference self, IGenericParameterProvider source)
		{
			foreach (var genericParam in source.GenericParameters)
			{
				self.GenericParameters.Add(new GenericParameter(genericParam.Name, self));
			}
		}

		//Instance methods are used to convert *Definition -> *Reference

		public static FieldReference Instance(this FieldDefinition self)
		{
			FieldReference result = new FieldReference(self.Name, self.FieldType);
			result.DeclaringType = self.DeclaringType.Instance();
			return result;
		}

		public static TypeReference Instance(this TypeDefinition self)
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

			var reference = new MethodReference(self.Name, self.ReturnType, declaringType);
			reference.HasThis = self.HasThis;
			reference.ExplicitThis = self.ExplicitThis;
			reference.CallingConvention = self.CallingConvention;

			foreach (var parameter in self.Parameters)
				reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

			return reference;
		}

		#region labels

		public static void SetLabel(this ILProcessor self, string label)
		{
			//OpCode[] AffectedOpCodes = { OpCodes.Br_S, OpCodes.Br, OpCodes.Brtrue, OpCodes.Brfalse, OpCodes.Brtrue_S, OpCodes.Brfalse_S };
			//bool nopMaked = false;

			//for (var i = 0; i < self.Body.Instructions.Count; i++)
			//{
			//    var inst = self.Body.Instructions[i];
			//    if (AffectedOpCodes.Contains(inst.OpCode) && inst.Operand.GetType() == typeof(Instruction) && ((Instruction)inst).OpCode == OpCodes.Ldstr && ((Instruction)inst.Operand).Operand == label)
			//    {
			//        if (!nopMaked)
			//        {
			//            self.Emit(OpCodes.Nop);
			//            nopMaked = true;
			//        }
			//        inst.Operand = self.Body.Instructions.Last();
			//    }
			//}
		}

		public static Instruction label(string name)
		{
			return Instruction.Create(OpCodes.Ldstr, name);
		}

		#endregion
	}
}
