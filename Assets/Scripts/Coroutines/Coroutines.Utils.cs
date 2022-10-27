//-----------------------------------------------------------------------------
// Copyright(c) 2016 Jean Simonet - http://jeansimonet.com
// Distributed under the MIT License - https://opensource.org/licenses/MIT
//-----------------------------------------------------------------------------
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Coroutines.Instructions;

namespace Coroutines
{
	public static class Utils
	{
		/// <summary>
		/// Waits for a given number of seconds
		/// </summary>
		public static CallInstruction WaitForSeconds(float seconds)
		{
			return ControlFlow.Call(WaitForSecondsCr(seconds));
		}

		/// <summary>
		/// Waits for a given number of frames
		/// </summary>
		public static CallInstruction WaitForFrames(int frames)
		{
			return ControlFlow.Call(WaitForFramesCr(frames));
		}

		/// <summary>
		/// Helper method to return whether any of the substates are true
		/// Used in the concurrent node to keep it running while any of the
		/// child nodes are running.
		/// </summary>
		public static bool Any(IEnumerable<bool> subStates)
		{
			return subStates.Any(sub => sub);
		}

		/// <summary>
		/// Helper method to return whether all of the substates are true
		/// </summary>
		public static bool All(IEnumerable<bool> subStates)
		{
			return subStates.All(sub => sub);
		}

		public static T FirstOrDefault<T>(IEnumerable<T> subStates)
		{
			return subStates.FirstOrDefault();
		}

		public static IEnumerable<Instruction<bool>> TrueWhileRunning(IEnumerable<Instruction> subroutine)
		{
			yield return TrueWhileRunningCall<bool>(subroutine);
		}

		public static IEnumerable<Instruction<U>> Adapt<T, U>(this IEnumerable<Instruction<T>> subroutine, System.Func<T, U> adapterMethod)
		{
			yield return AdapterCall(subroutine, adapterMethod);
		}

		/// <summary>
		/// Adapts a call from one Coroutine type to another, allowing the user to specify the conversion method
		/// </summary>
		public static ForwardInstruction<U> Adapt<T, U>(TransferFlowInstruction<T> instruction, System.Func<T, U> conversionMethod)
		{
			return new ForwardInstruction<U>(new Adapt<T, U>(instruction.ChildNodeT, conversionMethod));
		}

		static IEnumerable<Instruction> WaitForSecondsCr(float seconds)
		{
			// Wait until the specified time has elapsed, making sure to NOT wait
			// if the passed in time is 0.0f
			float startTime = Time.time;
			while (Time.time - startTime < seconds)
			{
				yield return null;
			}
		}


		static IEnumerable<Instruction> WaitForFramesCr(int frames)
		{
			// Wait until the specified number of frames have passed
			for (int i = 0; i < frames; ++i)
			{
				yield return null;
			}
		}

		/// <summary>
		/// Wraps an untyped coroutine in a bool typed coroutine whose value is true while
		/// the underlying coroutine is running, and false as soon as it completes.
		/// </summary>
		static ForwardInstruction<bool> TrueWhileRunningCall<T>(IEnumerable<Instruction> subroutine)
		{
			var coroutine = new Coroutine(subroutine);
			return new ForwardInstruction<bool>(new TrueWhileRunning(coroutine));
		}

		/// <summary>
		/// Wraps a typed coroutine in another type, given a conversion method
		/// </summary>
		static ForwardInstruction<U> AdapterCall<T, U>(IEnumerable<Instruction<T>> subroutine, System.Func<T, U> adapterMethod)
		{
			var sourceNode = new Coroutine<T>(subroutine);
			return new ForwardInstruction<U>(new Adapt<T, U>(sourceNode, adapterMethod));
		}

	}
}
