//-----------------------------------------------------------------------------
// Copyright(c) 2016 Jean Simonet - http://jeansimonet.com
// Distributed under the MIT License - https://opensource.org/licenses/MIT
//-----------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using Coroutines.Instructions;
using System.Linq;

namespace Coroutines
{
	public static class ControlFlow
	{
		/// <summary>
		/// Passes execution to a subroutine, then resumes the current coroutine
		/// </summary>
		public static CallInstruction Call(IEnumerable<Instruction> subroutine)
		{
			return new CallInstruction(new Coroutine(subroutine));
		}

		/// <summary>
		/// Passes execution to a subroutine, then resumes the current coroutine
		/// </summary>
		public static CallInstruction<T> Call<T>(IEnumerable<Instruction<T>> subroutine)
		{
			return new CallInstruction<T>(new Coroutine<T>(subroutine));
		}

		/// <summary>
		/// Passes execution to a subroutine, then resumes the current coroutine
		/// </summary>
		public static CallInstruction<T> Call<T, RT>(IEnumerable<RT> subroutine)
			where RT : Instruction<T>
		{
			return new CallInstruction<T>(new Coroutine<T>(subroutine.Cast<Instruction<T>>()));
		}

		/// <summary>
		/// Passes execution to a subroutine, then resumes the current coroutine
		/// While the subroutine executes though, its current 'Value' will be passed up the call chain
		/// </summary>
		public static ForwardInstruction<T> Forward<T>(this CallInstruction<T> callInstruction)
		{
			return new ForwardInstruction<T>(callInstruction.ChildNodeT);
		}

		/// <summary>
		/// Execute multiple subroutines at once, completes when all have completed
		/// Takes in a method to resolve the value of the concurrent node itself based
		/// on the values of the child nodes.
		/// </summary>
		public static CallInstruction<T> ConcurrentCall<T>(System.Func<IEnumerable<T>, T> forwardedValueResolution, params IEnumerable<Instruction<T>>[] subroutinesSources)
		{
			var subroutines = new List<ICoroutine<T>>();
			foreach (var subroutineSource in subroutinesSources)
			{
				subroutines.Add(new Coroutine<T>(subroutineSource));
			}
			return new CallInstruction<T>(new Concurrent<T>(forwardedValueResolution, subroutines));
		}

		/// <summary>
		/// Execute multiple subroutines at once, completes when all have completed
		/// </summary>
		public static CallInstruction ConcurrentCall<T>(params IEnumerable<Instruction<T>>[] subroutinesSources)
		{
			var subroutines = new List<ICoroutine<T>>();
			foreach (var subroutineSource in subroutinesSources)
			{
				subroutines.Add(new Coroutine<T>(subroutineSource));
			}
			return new CallInstruction(new Concurrent<T>(Utils.FirstOrDefault, subroutines));
		}

		/// <summary>
		/// Execute multiple subroutines at once, completes when all have completed
		/// </summary>
		public static CallInstruction ConcurrentCall(params IEnumerable<Instruction>[] subroutinesSources)
		{
			var subroutines = new List<ICoroutine>();
			foreach (var subroutineSource in subroutinesSources)
			{
				subroutines.Add(new Coroutine(subroutineSource));
			}
			return new CallInstruction(new Concurrent(Utils.Any, subroutines));
		}

		/// <summary>
		/// Executes The passed in node for as long as the condition is true
		/// Note, because of this, the master coroutine will be wrapped in a State node, that is,
		/// if it ever completes, it will be restarted.
		/// </summary>
		public static CallInstruction ExecuteWhile(System.Func<bool> masterCondition, IEnumerable<Instruction> slaveSubroutine)
		{
			var slaveCoroutineNode = new Coroutine(slaveSubroutine);
			return new CallInstruction(new While(slaveCoroutineNode, masterCondition));
		}

		/// <summary>
		/// Executes multiple subroutines at once, for as long as a condition is true
		/// </summary>
		public static CallInstruction ExecuteWhile(System.Func<bool> masterCondition, params IEnumerable<Instruction>[] slaveSubroutines)
		{
			// Create a concurrent node with all the passed in subroutines as children
			// And only consider the concurrent subroutine complete when all children are complete.
			var subroutines = new List<ICoroutine>();
			foreach (var subroutineSource in slaveSubroutines)
			{
				subroutines.Add(new Coroutine(subroutineSource));
			}
			var concurrentNode = new Concurrent(Utils.Any, subroutines);

			// Then pass that to a While node with the original condition
			return new CallInstruction(new While(concurrentNode, masterCondition));
		}

		/// <summary>
		/// Executes two subroutines at once, completes when the Master condition is true
		/// Note, because of this, the master coroutine will be wrapped in a State node, that is,
		/// if it ever completes, it will be restarted.
		/// </summary>
		public static CallInstruction<T> ExecuteWhile<T>(IEnumerable<Instruction<T>> masterStateCoroutine, System.Func<T, bool> masterCondition, IEnumerable<Instruction> slaveSubroutine)
		{
			var masterCoroutineState = new StateCoroutine<T>(masterStateCoroutine);
			var slaveCoroutineNode = new Coroutine(slaveSubroutine);
			return new CallInstruction<T>(new While<T>(masterCoroutineState, slaveCoroutineNode, masterCondition));
		}

		/// <summary>
		/// Executes multiple subroutines at once, with one master and multiple slaves.
		/// </summary>
		public static CallInstruction<T> ExecuteWhile<T>(IEnumerable<Instruction<T>> masterStateCoroutine, System.Func<T, bool> masterCondition, params IEnumerable<Instruction>[] slaveSubroutines)
		{
			var masterCoroutineState = new StateCoroutine<T>(masterStateCoroutine);

			// Create a concurrent node with all the passed in subroutines as children
			// And only consider the concurrent subroutine complete when all children are complete.
			var subroutines = new List<ICoroutine<bool>>();
			foreach (var subroutineSource in slaveSubroutines)
			{
				subroutines.Add(new TrueWhileRunning(new Coroutine(subroutineSource)));
			}
			var concurrentNode = new Concurrent<bool>(Utils.Any, subroutines);

			return new CallInstruction<T>(new While<T>(masterCoroutineState, concurrentNode, masterCondition));
		}

		/// <summary>
		/// Executes two subroutines at once, completes when the Master one completes, regardless
		/// of the state of the Slave coroutine
		/// </summary>
		public static CallInstruction ExecuteWhileRunning(IEnumerable<Instruction> masterSubroutine, IEnumerable<Instruction> slaveSubroutine)
		{
			var masterCoroutineNode = new Coroutine(masterSubroutine);
			var masterCoroutineState = new TrueWhileRunning(masterCoroutineNode);
			var slaveCoroutineNode = new Coroutine(slaveSubroutine);
			return new CallInstruction(new While<bool>(masterCoroutineState, slaveCoroutineNode, state => state));
		}

		/// <summary>
		/// Executes multiple subroutines at once, with one master and multiple slaves.
		/// Completes when the Master one completes, regardless of the state of the Slave coroutine
		/// </summary>
		public static CallInstruction ExecuteWhileRunning(IEnumerable<Instruction> masterSubroutine, params IEnumerable<Instruction>[] slaveSubroutines)
		{
			var masterCoroutineNode = new Coroutine(masterSubroutine);
			var masterCoroutineState = new TrueWhileRunning(masterCoroutineNode);

			// Create a concurrent node with all the passed in subroutines as children
			// And only consider the concurrent subroutine complete when all children are complete.
			var subroutines = new List<ICoroutine>();
			foreach (var subroutineSource in slaveSubroutines)
			{
				subroutines.Add(new Coroutine(subroutineSource));
			}
			var concurrentNode = new Concurrent(Utils.Any, subroutines);

			return new CallInstruction(new While<bool>(masterCoroutineState, concurrentNode, state => state));
		}

		/// <summary>
		/// Helper method that returns a concurrent node, wrapped in a coroutine, so they can be chained in source code
		/// </summary>
		static IEnumerable<Instruction> ConcurrentCoroutinesHelper(IEnumerable<Instruction>[] subroutines)
		{
			yield return ConcurrentCall(subroutines);
		}

	}

}
