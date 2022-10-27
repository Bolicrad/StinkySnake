//-----------------------------------------------------------------------------
// Copyright(c) 2016 Jean Simonet - http://jeansimonet.com
// Distributed under the MIT License - https://opensource.org/licenses/MIT
//-----------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Coroutines
{
	/// <summary>
	/// Base class for coroutine return values
	/// Framework instructions derive from this. They tell the runtime to do something
	/// before returning execution to this coroutine, like executing another coroutine to
	/// completion for instance. This is an abstract class and not an interface because
	/// we want to be able to implicitely cast from Instructions to result instructions (below)
	/// And that's something we can only do between actual types, not interfaces
	/// </summary>
	public abstract class Instruction
	{
		// The base class doesn't offer any specific behaviour
	}

	/// <summary>
	/// Stores strongly typed result for coroutines.
	/// Interpretation depends on the execution node that processes it. It doesn't
	/// derive from the non-generic Instruction on purpose. Instead we allow an implicit cast
	/// from non-generic Instruction to Instruction<T>. This gives us the ability to force the
	/// user to declare their coroutines to be of type IEnumerable<Instruction<T>>.
	/// </summary>
	public class Instruction<T>
	{
		// A Null instruction necessarily means there is a valid value
		public Instruction BaseInstruction
		{
			get;
			private set;
		}

		// The validity of this member is dependent on the Instruction
		public T Value
		{
			get;
			private set;
		}

		public bool IsValue
		{
			get
			{
				return BaseInstruction == null;
			}
		}

		public bool IsInstruction
		{
			get
			{
				return BaseInstruction != null;
			}
		}

		public Instruction()
		{
			BaseInstruction = Instructions.NoopInstruction.Noop;
			Value = default(T);
		}

		public Instruction(T returnValue)
		{
			Value = returnValue;
			BaseInstruction = null;
		}

		public Instruction(Instructions.TransferFlowInstruction<T> instruction)
		{
			BaseInstruction = instruction;
			Value = default(T);
		}

		public Instruction(Instructions.CallInstruction instruction)
		{
			BaseInstruction = instruction;
			Value = default(T);
		}

		public static implicit operator Instruction<T>(Instructions.TransferFlowInstruction<T> instruction)
		{
			return new Instruction<T>(instruction);
		}

		public static implicit operator Instruction<T>(Instructions.CallInstruction instruction)
		{
			return new Instruction<T>(instruction);
		}

		public static implicit operator Instruction<T>(T result)
		{
			return new Instruction<T>(result);
		}
	}

	namespace Instructions
	{
		/// <summary>
		/// Defines no instruction!
		/// </summary>
		public class NoopInstruction
			: Instruction
		{
			// Doesn't need to do anything
			public static NoopInstruction Noop = new NoopInstruction();
		}

		/// <summary>
		/// Tells the coroutine to reset execution from the begining
		/// </summary>
		public class ResetInstruction
			: Instruction
		{
			// Doesn't need to do anything
			public static ResetInstruction Reset = new ResetInstruction();
		}

		/// <summary>
		/// base class for all control flow instructions
		/// </summary>
		public class TransferFlowInstruction
			: Instruction
		{
			public ICoroutine ChildNode;
			public TransferFlowInstruction(ICoroutine childNode)
			{
				ChildNode = childNode;
			}
		}

		/// <summary>
		/// Used to tell the framework to pass control to a sub coroutine, and
		/// resume after it is complete.
		/// </summary>
		public class CallInstruction
			: TransferFlowInstruction
		{
			public CallInstruction(ICoroutine childNode)
				: base(childNode)
			{
			}
		}

		/// <summary>
		/// base class for all control flow instructions
		/// </summary>
		public class TransferFlowInstruction<T>
			: TransferFlowInstruction
		{
			public ICoroutine<T> ChildNodeT
			{
				get
				{
					// We know this is okay because we initialize the base class ourselves
					return ChildNode as ICoroutine<T>;
				}
			}

			public TransferFlowInstruction(ICoroutine<T> childNode)
				: base(childNode)
			{
			}
		}


		/// <summary>
		/// Used to tell the framework to pass control to a sub coroutine, and
		/// resume after it is complete. This is for strongly types coroutines,
		/// and while the subroutine is executed, the calling coroutine is NOT
		/// returning any new value to ITS caller.
		/// </summary>
		public class CallInstruction<T>
			: TransferFlowInstruction<T>
		{
			public CallInstruction(ICoroutine<T> childNode)
				: base(childNode)
			{
			}
		}

		/// <summary>
		/// Used to tell the framework to pass control to a sub coroutine, and
		/// resume after it is complete. This is for strongly types coroutines,
		/// and in this case, the returned result is passed on to the caller.
		/// This is useful to make pass-through coroutines that in essence take
		/// the value of the subroutine they are calling. This is especially useful
		/// for States and Behaviour graphs.
		/// </summary>
		public class ForwardInstruction<T>
			: TransferFlowInstruction<T>
		{
			public ForwardInstruction(ICoroutine<T> childNode)
				: base(childNode)
			{
			}
		}
	}
}