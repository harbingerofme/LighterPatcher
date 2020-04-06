using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LighterPatcher
{
    class MethodContainer
    {
        public MethodDefinition Method;
        public HashSet<Instruction> Instructions = new HashSet<Instruction>();

        public MethodContainer(MethodDefinition method, Instruction instruction)
        {
            Method = method;

            if (!Instructions.Any(instruction1 => instruction1.Operand.Equals(instruction.Operand)))
            {
                Instructions.Add(instruction);
            }
        }

        public void AddInstruction(Instruction instruction)
        {
            if (!Instructions.Any(instruction1 => instruction1.Operand.Equals(instruction.Operand)))
            {
                Instructions.Add(instruction);
            }
        }

        public override int GetHashCode()
        {
            return Method.FullName.GetHashCode() + Instructions.Count;
        }
    }
}
