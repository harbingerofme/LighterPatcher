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
        public HashSet<string> Operands = new HashSet<string>();

        public MethodContainer(MethodDefinition method, Instruction instruction)
        {
            Method = method;
            string instructionOperand = instruction.Operand.ToString();
            Operands.Add(instructionOperand);

        }

        public void AddInstruction(Instruction instruction)
        {
            string instructionOperand = instruction.Operand.ToString();
            if (!Operands.Contains(instructionOperand))
            {
                Operands.Add(instructionOperand);
            }
        }

        /*
        public override int GetHashCode()
        {
            return Method.FullName.GetHashCode() + Instructions.Count;
        }
        */
        public int MakeHashCode()
        {
            return Method.FullName.GetHashCode() + Operands.Count;
        }
    }
}
