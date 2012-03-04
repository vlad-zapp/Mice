using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Reflection;

namespace Mice.Addons
{
	public class ILBuilder
	{
		private readonly ILProcessor proc;
		public Dictionary<string, object> known_objects = new Dictionary<string, object>();
		public Dictionary<string, Instruction> labels = new Dictionary<string, Instruction>();
		OpCode[] AffectedOpCodes = { OpCodes.Br_S, OpCodes.Br, OpCodes.Brtrue, OpCodes.Brfalse, OpCodes.Brtrue_S, OpCodes.Brfalse_S };


		#region ctors
		/// <summary>
		/// Returns new il emitter, binded to some methods code.
		/// </summary>
		/// <param name="sourceMethod">method, which code body will be processed</param>
		public ILBuilder(MethodDefinition sourceMethod)
		{
			proc = sourceMethod.Body.GetILProcessor();
		}

		/// <summary>
		/// returns new il emitter, binded to some ILProcessor
		/// </summary>
		/// <param name="codeProcessor"></param>
		public ILBuilder(ILProcessor codeProcessor)
		{
			proc = codeProcessor;
		}
		#endregion

		public void Parse(string source)
		{
			source = new Regex("[ |\t]*//.*\n").Replace(source, "\n"); //delete comments
			source = new Regex(" [ ]").Replace(source, " ").Replace("\t", "").Replace("\r", ""); //delete junk spaces and tabs

			var lines = source.Split('\n').Where(l => !(new Regex("^[ |\t]*$")).IsMatch(l)).ToArray(); //filter empty lines

			foreach (var line in lines)
				EmitLine(line);

			ResolveLabels();
		}

		private void EmitLine(string line)
		{
			if (ProcessMacros(line))
				return;

			var stringInst = line.Split(' ');
			if (stringInst.Length > 2)
				throw new ArgumentException("Each code string should have only opcode and (maybe) operand, separateded by the space.");

			if (stringInst[0].Last() == ':')
			{
				CreateLabel(stringInst[0]);
				return;
			}

			OpCode code = ParseOpcode(stringInst[0]);

			Instruction inst;
			if (stringInst.Length == 2)
			{
				inst = ParseArgument(code, stringInst[1]);
				//dirty hack
				//inst = Instruction.Create(code, arg);
			}
			else
				inst = Instruction.Create(code);

			proc.Append(inst);
		}

		private OpCode ParseOpcode(string line)
		{
			OpCode code;
			var OpCodeRef = typeof(OpCodes).GetFields().SingleOrDefault(m => m.Name.ToLower() == line.ToLower().Replace('.', '_'));

			if (OpCodeRef == null)
				throw new ArgumentException(String.Format("Opcode \"{0}\" is invalid", line));
			else
				code = (OpCode)(typeof(OpCodes).GetField(OpCodeRef.Name).GetValue(null));
			return code;
		}

		private Instruction ParseArgument(OpCode code, string source)
		{
			if (code.OperandType == OperandType.InlineBrTarget)
			{
				return Instruction.Create(code,Instruction.Create(OpCodes.Ldstr, "goto-label:" + source));
			}
			else if (code.OperandType == OperandType.InlineArg)
			{
				//return Instruction.Create(OpCodes.Ldarg);//int.Parse(source));
				int ret = 0;
				if (int.TryParse(source, out ret))
					return proc.Create(OpCodes.Ldarg_0);//proc.Create(code, ret);
				else
					return proc.Create(code, source);
			}

			var known_object = known_objects[source];

			if (known_object == null)
				throw new ArgumentException(String.Format("{0} is unknown", source));

			if (code.OperandType == OperandType.InlineMethod)
			{
				return Instruction.Create(code,((MethodDefinition)known_object).Instance());
			}
			else if (code.OperandType == OperandType.InlineField)
			{
				return Instruction.Create(code,((FieldDefinition)known_object).Instance());
			}
			else
				throw new NotSupportedException("Sorry, but that kind of argument is not supported yet!");
		}

		private void CreateLabel(string name)
		{
			labels.Add("goto-label:" + name.Substring(0, name.Length - 1), proc.Body.Instructions.Last());
		}

		private void ResolveLabels()
		{
			var links = from i in proc.Body.Instructions
						where i.Operand != null
						&& i.Operand.GetType() == typeof(Instruction)
						&& ((Instruction)i.Operand).Operand != null
						&& ((Instruction)i.Operand).Operand.GetType() == typeof(string)
						&& (((string)((Instruction)i.Operand).Operand).StartsWith("goto-label"))
						select new { key = ((Instruction)i.Operand).Operand as string, instruction = i };

			foreach (var link in links)
			{
				if (labels.ContainsKey(link.key))
				{
					var g2 = labels[link.key].Next != null ? labels[link.key].Next : labels[link.key];
					link.instruction.Operand = g2;
				}
				else
				{
					throw new Exception(String.Format("The link {0} is not defined", link.key));
				}
			}
		}

		private bool ProcessMacros(string line)
		{
			//var singleNumber = new Regex(@"\[{[\d]+}\]");
			var numberRange = new Regex(@"\[(\d+)-(\d+)\]");

			if (numberRange.IsMatch(line))
			{
				var from = int.Parse(numberRange.Match(line).Groups[1].Value);
				var to = int.Parse(numberRange.Match(line).Groups[2].Value);

				if (to < from)
					throw new ArgumentException(String.Format("incorrect range {0}-{1}", from, to));

				foreach (var num in Enumerable.Range(from, to))
					EmitLine(numberRange.Replace(line, num.ToString()));
				return true;
			}
			return false;
		}
	}
}
