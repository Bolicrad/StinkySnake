//-----------------------------------------------------------------------------
// Copyright(c) 2016 Jean Simonet - http://jeansimonet.com
// Distributed under the MIT License - https://opensource.org/licenses/MIT
//-----------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System;

namespace Coroutines
{
	/// <summary>
	/// Defines an execution node, that other execution nodes can control and query
	/// </summary>
	public interface ICoroutine
		: System.IDisposable
	{
		bool IsRunning { get; }
		void Update();
		void Reset();
	}

	/// <summary>
	/// Only need the implementers of the ICoroutine class need to define IsRunning,
	/// We'll define IsComplete as an extension!
	/// </summary>
	public static class ICoroutineExtensions
	{
		// If a node is not running, it is necessarily complete
		public static bool IsComplete(this ICoroutine node)
		{
			return !node.IsRunning;
		}
	}

	/// <summary>
	/// Defines a strongly typed execution node, useful for custom node types
	/// Where a node might need to query the state of a child node beyond just running/complete
	/// (such as behaviour graph nodes for instance)
	/// </summary>
	public interface ICoroutine<T>
		: ICoroutine
	{
		// Stores whether the node has a value, since the value type (T) maybe not be nullable (and there is
		// no way to partially specialize the generic for value types)
		bool HasValue { get; }

		// Stores the 'last' value returned by the coroutine, if any
		T Value { get; }
	}

	/// <summary>
	/// The meat and potato node for our coroutine framework
	/// Implements a coroutine without a specific return type, meaning that
	/// Parent nodes/coroutines can not check it's state beyong running/complete
	/// </summary>
	public class Coroutine
		: ICoroutine
	{
		// This stores the source enumerable
		IEnumerable<Instruction> _Source;

		// This stores a running instance of the coroutine
		IEnumerator<Instruction> _StateData;

		// If we are told to yield execution to a sub node, store it here!
		ICoroutine _Subroutine;

		// Current state of this node
		public enum CoroutineState
		{
			// The coroutine is actively doing things
			Running,

			// The coroutine has passed execution to a child node
			Subroutine,

			// The coroutine isn't doing anything anymore
			Complete,

			// The coroutine has been cleaned up, it shouldn't be used
			Disposed
		}
		CoroutineState _ExecutionState;

		// Implement the IsRunning property, of course
		public bool IsRunning
		{
			get
			{
				Debug.Assert(_ExecutionState != CoroutineState.Disposed);
				return _ExecutionState == CoroutineState.Running || _ExecutionState == CoroutineState.Subroutine;
			}
		}

		/// <summary>
		/// Only provided constructor needs a iterator block of course
		/// </summary>
		public Coroutine(IEnumerable<Instruction> source)
		{
			// Start processing right away
			_Source = source;
			_StateData = source.GetEnumerator();
			_ExecutionState = CoroutineState.Running;
			_Subroutine = null;
		}

		public void Dispose()
		{
			// Clean up
			if (_Subroutine != null)
			{
				_Subroutine.Dispose();
				_Subroutine = null;
			}

			if (_StateData != null)
			{
				_StateData.Dispose();
				_StateData = null;
			}

			_Source = null;

			// Disposing forces us to be "complete"
			_ExecutionState = CoroutineState.Disposed;
		}

		public void Reset()
		{
			Debug.Assert(_ExecutionState != CoroutineState.Disposed);

			// Clean up any subroutine call
			// Since we're forgetting about the subroutine,
			// We need to make sure and clean it up
			if (_Subroutine != null)
			{
				_Subroutine.Dispose();
				_Subroutine = null;
			}

			// Reset our enumerator
			_StateData.Dispose();
			_StateData = _Source.GetEnumerator();

			// We are running again
			_ExecutionState = CoroutineState.Running;
		}

		/// <summary>
		/// Executes the coroutine until the next yield statement
		/// Possibly recursive Update method, it will recurse when iteration on a child terminates for instance,
		/// so we don't skip a single frame if we don't need to.
		/// </summary>
		public void Update()
		{
			switch (_ExecutionState)
			{
				case CoroutineState.Running:
					{
						Debug.Assert(_Subroutine == null);
						try
						{
							if (_StateData.MoveNext())
							{
								var ret = _StateData.Current;

								// Decide what to do based on what the return value is!
								if (ret == null || ret is Instructions.NoopInstruction)
								{
									// Nothing to do, we'll execute the coroutine again next update!
								}
								else if (ret is Instructions.ResetInstruction)
								{
									// Reset our enumerator
									_StateData.Dispose();
									_StateData = _Source.GetEnumerator();
								}
								else
								{
									var callInstruction = ret as Instructions.TransferFlowInstruction;
									if (callInstruction != null)
									{
										// The user code says that we should pass execution to a subroutine, and then resume the current coroutine
										// set up our child and execute it right away
										_Subroutine = callInstruction.ChildNode;
										_ExecutionState = CoroutineState.Subroutine;

										// Update the subroutine, and if it completes right away, then we should continue execution of THIS coroutine until the next yield instruction
										// We'll deal with this recursively
										Update();
									}
									else
									{
										// Not handled yet
										throw new System.Exception("Instruction of type " + ret.GetType().ToString() + " not supported by Coroutine");
									}
								}
							}
							else
							{
								// We reached the end of the enumeration (i.e. the end of the code)
								// We could either loop back around (get a new enumerator), or do nothing...
								// We'll do nothing by default. Much better to force the user to write
								// an infinite loop if that's what they want.
								_ExecutionState = CoroutineState.Complete;
							}
						}
						finally{}
						// catch (Diagnostics.CoroutineException gex)
						// {
						// 	throw Diagnostics.CoroutineException.MakeException(gex, _StateData);
						// }
						// catch (System.Exception ex)
						// {
						// 	throw Diagnostics.CoroutineException.MakeException(ex, _StateData);
						// }
					}
					break;
				case CoroutineState.Subroutine:
					{
						Debug.Assert(_StateData != null);
						// We have a subroutine we are 'waiting' on, so execute it instead
						_Subroutine.Update();
						if (_Subroutine.IsComplete())
						{
							// Clean up the subroutine
							_Subroutine.Dispose();
							_Subroutine = null;

							// Resume THIS coroutine
							_ExecutionState = CoroutineState.Running;

							// to resume the subroutine properly and handle all the cases, just recurse
							Update();
						}
					}
					break;
				case CoroutineState.Complete:
					{
						Debug.Assert(_Subroutine == null);
						// Do nothing
					}
					break;
				case CoroutineState.Disposed:
					throw new System.Exception("Coroutine has been disposed, and should not be updated again");
				default:
					throw new System.NotImplementedException("Unhandled coroutine execution state");
			}
		}
	}

	/// <summary>
	/// Base class for strongly typed Coroutines that can give more information about their current state
	/// to their parent node. For instance this will be used for Behaviour Tree nodes
	/// </summary>
	[System.Serializable]
	public class Coroutine<T>
		: ICoroutine<T>
	{
		// This stores the source enumerable
		IEnumerable<Instruction<T>> _Source;

		// This stores a running instance of the coroutine
		IEnumerator<Instruction<T>> _StateData;

		// If we are told to yield execution to a sub node, store it here!
		ICoroutine _Subroutine;

		// Current state of this node
		public enum CoroutineState
		{
			// The coroutine is actively doing things
			Running,

			// The coroutine has passed execution to a child node
			SubroutineCall,

			// The coroutine has passed execution to a child node
			SubroutineForward,

			// The coroutine isn't doing anything anymore
			Complete,

			// The coroutine has been cleaned up, it shouldn't be used
			Disposed
		}
		CoroutineState _ExecutionState;

		bool _HasValue;
		T _Value;

		public bool IsRunning
		{
			get
			{
				return _ExecutionState == CoroutineState.Running || _ExecutionState == CoroutineState.SubroutineCall || _ExecutionState == CoroutineState.SubroutineForward;
			}
		}

		/// <summary>
		/// Stores whether the node has a value, since the value type (T) maybe not be nullable (and there is
		/// no way to partially specialize the generic for value types)
		/// </summary>
		public bool HasValue
		{
			get
			{
				return _HasValue;
			}
		}

		/// <summary>
		/// Stores the 'last' value returned by the coroutine, if any
		/// </summary>
		public T Value
		{
			get
			{
				return _Value;
			}
		}

		public Coroutine(IEnumerable<Instruction<T>> source)
		{
			_Source = source;
			_StateData = source.GetEnumerator();
			_HasValue = false;
			_Value = default(T);
			_ExecutionState = CoroutineState.Running;
		}

		public void Dispose()
		{
			// Clean up
			if (_Subroutine != null)
			{
				_Subroutine.Dispose();
				_Subroutine = null;
			}

			if (_StateData != null)
			{
				_StateData.Dispose();
				_StateData = null;
			}

			_Source = null;

			// Disposing forces us to be "complete"
			_ExecutionState = CoroutineState.Disposed;
		}

		public void Reset()
		{
			Debug.Assert(_ExecutionState != CoroutineState.Disposed);

			// Clean up any subroutine call
			// Since we're forgetting about the subroutine,
			// We need to make sure and clean it up
			if (_Subroutine != null)
			{
				_Subroutine.Dispose();
				_Subroutine = null;
			}

			// Reset our enumerator
			_StateData.Dispose();
			_StateData = _Source.GetEnumerator();

			// We are running again
			_ExecutionState = CoroutineState.Running;
		}

		/// <summary>
		/// Executes the coroutine until the next yield statement
		/// </summary>
		public void Update()
		{
			switch (_ExecutionState)
			{
				case CoroutineState.Running:
					{
						Debug.Assert(_Subroutine == null);
						try
						{
							if (_StateData.MoveNext())
							{
								var res = _StateData.Current;

								if (res != null)
								{
									var coresult = res as Instruction<T>;
									if (coresult != null)
									{
										if (coresult.IsInstruction)
										{
											if (coresult.BaseInstruction is Instructions.NoopInstruction)
											{
												// Nothing to do this frame
											}
											else if (coresult.BaseInstruction is Instructions.ResetInstruction)
											{
												// Reset our enumerator
												_StateData.Dispose();
												_StateData = _Source.GetEnumerator();
											}
											else
											{
												var flowInstruction = coresult.BaseInstruction as Instructions.TransferFlowInstruction;
												if (flowInstruction != null)
												{
													// Two special cases, forward and call
													if (flowInstruction is Instructions.ForwardInstruction<T>)
													{
														// Switch to the forward state
														_Subroutine = flowInstruction.ChildNode;
														_ExecutionState = CoroutineState.SubroutineForward;

														// Recurse to make sure we process child right away and handle all cases
														Update();
													}
													else
													{
														// Regular call, the return values, if any, are ignored
														_Subroutine = flowInstruction.ChildNode;
														_ExecutionState = CoroutineState.SubroutineCall;

														// Recurse to make sure we process child right away and handle all cases
														Update();
													}
												}
												else
												{
													throw new System.NotImplementedException("Unknown instruction type");
												}
											}
										}
										else
										{
											// Set (or replace the value, it will stay until we either reset the coroutine or change it)
											_HasValue = true;
											_Value = coresult.Value;
										}
									}
									else
									{
										// Everything passed in to this should either be null or a Result
										throw new System.Exception("Coroutine<> should always get Result types!");
									}
								}
								// Else nothing to do this frame
							}
							else
							{
								// We reached the end of the enumeration (i.e. the end of the code)
								// We could either loop back around (get a new enumerator), or do nothing...
								// We'll do nothing by default. Much better to force the user to write
								// an infinite loop if that's what they want.
								_ExecutionState = CoroutineState.Complete;
							}
						}
						finally{}
						// catch (Diagnostics.CoroutineException gex)
						// {
						// 	throw Diagnostics.CoroutineException.MakeException(gex, _StateData);
						// }
						// catch (System.Exception ex)
						// {
						// 	throw Diagnostics.CoroutineException.MakeException(ex, _StateData);
						// }
					}
					break;
				case CoroutineState.SubroutineCall:
					{
						Debug.Assert(_StateData != null);
						// We have a subroutine we are 'waiting' on, so execute it instead
						_Subroutine.Update();
						if (_Subroutine.IsComplete())
						{
							// Clean up the subroutine
							_Subroutine.Dispose();
							_Subroutine = null;

							// Resume THIS coroutine
							_ExecutionState = CoroutineState.Running;

							// to resume the subroutine properly and handle all the cases, just recurse
							Update();
						}
					}
					break;
				case CoroutineState.SubroutineForward:
					{
						Debug.Assert(_StateData != null);
						// We have a subroutine we are 'waiting' on, so execute it instead, and forward values
						var subroutine = _Subroutine as ICoroutine<T>;

						// Update the subroutine
						subroutine.Update();

						// Grab the returned value, if any
						if (subroutine.HasValue)
						{
							_HasValue = true;
							_Value = subroutine.Value;
						}

						// Handle completion
						if (_Subroutine.IsComplete())
						{
							// Clean up the subroutine
							_Subroutine.Dispose();
							_Subroutine = null;

							// Resume THIS coroutine
							_ExecutionState = CoroutineState.Running;

							// to resume the subroutine properly and handle all the cases, just recurse
							Update();
						}
					}
					break;
				case CoroutineState.Complete:
					{
						Debug.Assert(_Subroutine == null);
						// Do nothing
					}
					break;
				case CoroutineState.Disposed:
					throw new System.Exception("Coroutine has been disposed, and should not be updated again");
				default:
					throw new System.NotImplementedException("Unhandled coroutine execution state");
			}
		}
	}

	/// <summary>
	/// Adapts a node's return type to another, just like Linq does it!
	/// So if you have a Coroutine<Vector3>, you can convert it to a Coroutine<float>
	/// and specify how to get a float from the Vector3.
	/// </summary>
	[System.Serializable]
	public class Adapt<T, U>
		: ICoroutine<U>
	{
		ICoroutine<T> _Subroutine;
		System.Func<T, U> _ConversionMethod;

		public bool HasValue
		{
			get
			{
				return _Subroutine.HasValue;
			}
		}

		public bool IsRunning
		{
			get
			{
				return _Subroutine.IsRunning;
			}
		}

		public U Value
		{
			get
			{
				// This is where we use the conversion, of course
				return _ConversionMethod(_Subroutine.Value);
			}
		}

		public Adapt(ICoroutine<T> subroutine, System.Func<T, U> conversionMethod)
		{
			_Subroutine = subroutine;
			_ConversionMethod = conversionMethod;
		}

		public void Dispose()
		{
			_Subroutine.Dispose();
			_ConversionMethod = null;
		}

		public void Reset()
		{
			_Subroutine.Reset();
		}

		public void Update()
		{
			_Subroutine.Update();
		}
	}

	/// <summary>
	/// Casts a Result node from one type to another
	/// </summary>
	[System.Serializable]
	public class Cast<T, U>
		: Adapt<T, U>
		where U : T
	{
		public Cast(ICoroutine<T> subroutine)
			: base (subroutine, (t) => (U)t)
		{
		}
	}

	/// <summary>
	/// Executes multiple subnodes concurrently, and arbitrating the 'running' state of this node
	/// based on a method passed in by the user
	/// </summary>
	public class Concurrent
		: ICoroutine
	{
		// This stores all the sub behaviours
		List<ICoroutine> _SubBehaviours;

		// This helps us resolve the running state of this node based on the running state of child nodes
		System.Func<IEnumerable<bool>, bool> _RunningResolutionFunc;

		enum ConcurrentState
		{
			Running,
			Complete,
			Disposed,
		}
		ConcurrentState _CurrentState;

		public bool IsRunning
		{
			get { return _CurrentState == ConcurrentState.Running; }
		}

		/// <summary>
		/// Constructor, pass in all the nodes you wish to execute concurrently,
		/// along with a function to sort out the state of this node.
		/// </summary>
		public Concurrent(System.Func<IEnumerable<bool>, bool> stateResolutionFunc, IEnumerable<ICoroutine> subBehaviours)
		{
			// Create our list, and initialize the running one with the behaviours we get passed
			_SubBehaviours = new List<ICoroutine>(subBehaviours);
			_RunningResolutionFunc = stateResolutionFunc;
			_CurrentState = ConcurrentState.Running;
		}

		/// <summary>
		/// Helper constructor, to manually pass arbitrary number of children
		/// </summary>
		public Concurrent(System.Func<IEnumerable<bool>, bool> stateResolutionFunc, params ICoroutine[] subBehaviours)
			: this(stateResolutionFunc, subBehaviours as IEnumerable<ICoroutine>)
		{
		}

		public void Update()
		{
			switch (_CurrentState)
			{
				case ConcurrentState.Running:
					{
						// Process all sub behaviours first
						for (int i = 0; i < _SubBehaviours.Count; ++i)
						{
							_SubBehaviours[i].Update();
						}

						// Use the resolution method to determine if we should terminate
						if (!_RunningResolutionFunc(_SubBehaviours.Select(sb => sb.IsRunning)))
						{
							_CurrentState = ConcurrentState.Complete;
							// Reset any node that was running
							for (int i = 0; i < _SubBehaviours.Count; ++i)
							{
								if (_SubBehaviours[i].IsRunning)
								{
									_SubBehaviours[i].Reset();
								}
							}
						}
					}
					break;
				case ConcurrentState.Complete:
					// Nothing to do
					break;
				case ConcurrentState.Disposed:
					throw new System.Exception("Concurrent node is disposed and should not be updated");
			}
		}

		public void Dispose()
		{
			// Clean up
			if (_SubBehaviours != null)
			{
				foreach (var subroutine in _SubBehaviours)
				{
					subroutine.Dispose();
				}
				_SubBehaviours = null;
			}
			_CurrentState = ConcurrentState.Disposed;
		}

		public void Reset()
		{
			for (int i = 0; i < _SubBehaviours.Count; ++i)
			{
				_SubBehaviours[i].Reset();
			}
			_CurrentState = ConcurrentState.Running;
		}
	}


	/// <summary>
	/// Executes multiple subnodes concurrently, and arbitrating the 'seen' result value of this node
	/// through a user-specified method. This is used by behaviour nodes for instance to say the concurrent
	/// node is "active" as long as one of the child nodes is active, or only if ALL the subnodes are active,
	/// it's all up to the user.
	/// </summary>
	public class Concurrent<T>
		: ICoroutine<T>
	{
		// This stores all the sub behaviours
		List<ICoroutine<T>> _SubBehaviours;

		// This helps us resolve the return value of this node based on the value of child concurrent nodes
		System.Func<IEnumerable<T>, T> _StateResolutionFunc;

		enum ConcurrentState
		{
			Running,
			Complete,
			Disposed,
		}
		ConcurrentState _CurrentState;

		public bool IsRunning
		{
			get { return _CurrentState == ConcurrentState.Running; }
		}

		/// <summary>
		/// Constructor, pass in all the nodes you wish to execute concurrently,
		/// along with a function to sort out the state of this node.
		/// </summary>
		public Concurrent(System.Func<IEnumerable<T>, T> stateResolutionFunc, IEnumerable<ICoroutine<T>> subBehaviours)
		{
			// Create our list, and initialize the running one with the behaviours we get passed
			_SubBehaviours = new List<ICoroutine<T>>(subBehaviours);
			_StateResolutionFunc = stateResolutionFunc;
			_CurrentState = ConcurrentState.Running;
		}

		/// <summary>
		/// Helper constructor, to manually pass arbitrary number of children
		/// </summary>
		public Concurrent(System.Func<IEnumerable<T>, T> stateResolutionFunc, params ICoroutine<T>[] subBehaviours)
			: this(stateResolutionFunc, subBehaviours as IEnumerable<ICoroutine<T>>)
		{
		}

		/// <summary>
		/// Returns whether this node has a resolved state
		/// </summary>
		public bool HasValue
		{
			get
			{
				return _SubBehaviours.Any(sub => sub.HasValue);
			}
		}

		/// <summary>
		/// Returns the resolved state of this node, based on the child nodes
		/// </summary>
		public T Value
		{
			get
			{
				// Pass all the children that have a valid return value to the resolution method
				return _StateResolutionFunc(_SubBehaviours.Where(sub => sub.HasValue).Select(sub => sub.Value));
			}
		}

		public void Update()
		{
			switch (_CurrentState)
			{
				case ConcurrentState.Running:
					{
						// Process all sub behaviours and store the results
						bool atLeastOneRunning = false;
						for (int i = 0; i < _SubBehaviours.Count; ++i)
						{
							_SubBehaviours[i].Update();
							if (_SubBehaviours[i].IsRunning)
							{
								atLeastOneRunning = true;
							}
						}

						if (!atLeastOneRunning)
						{
							_CurrentState = ConcurrentState.Complete;
						}
					}
					break;
				case ConcurrentState.Complete:
					// Nothing to do
					break;
				case ConcurrentState.Disposed:
					throw new System.Exception("Concurrent node is disposed and should not be updated");
			}
		}

		public void Dispose()
		{
			// Clean up
			if (_SubBehaviours != null)
			{
				foreach (var subroutine in _SubBehaviours)
				{
					subroutine.Dispose();
				}
				_SubBehaviours = null;
			}
			_CurrentState = ConcurrentState.Disposed;
		}

		public void Reset()
		{
			for (int i = 0; i < _SubBehaviours.Count; ++i)
			{
				_SubBehaviours[i].Reset();
			}
			_CurrentState = ConcurrentState.Running;
		}
	}

	/// <summary>
	/// Executes the slave node for as long as the master condition is true,
	/// As soon as it isn't, stop executing the slave node
	/// </summary>
	[System.Serializable]
	public class While
		: ICoroutine
	{
		System.Func<bool> _MasterCondition;
		ICoroutine _SlaveSubroutine; // Doesn't have to be a state, it 'can' terminate!

		enum WhileNodeState
		{
			Running,
			Complete,
			Disposed
		}
		WhileNodeState _CurrentState;

		public While(ICoroutine slaveNode, System.Func<bool> masterCondition)
		{
			// Create our slave subroutine
			_SlaveSubroutine = slaveNode;

			// Store the condition function
			_MasterCondition = masterCondition;

			// We start running
			_CurrentState = WhileNodeState.Running;
		}

		public bool IsRunning
		{
			get
			{
				return _CurrentState == WhileNodeState.Running;
			}
		}

		public void Update()
		{
			Debug.Assert(_MasterCondition != null);
			Debug.Assert(_SlaveSubroutine != null);

			switch (_CurrentState)
			{
				case WhileNodeState.Running:
					{
						if (!_MasterCondition())
						{
							// Condition is false now, reset the slave (interrupt it)
							_SlaveSubroutine.Reset();
							_CurrentState = WhileNodeState.Complete;
						}
						else
						{
							// Master is not finished, continue updating the slave, if applicable
							// Note that this doesn't automatically reset the slave!
							_SlaveSubroutine.Update();
						}
					}
					break;
				case WhileNodeState.Complete:
					// Nothing to do
					break;
				case WhileNodeState.Disposed:
					throw new System.Exception("MasterSlave node is disposed and should not be updated");
			}
		}

		public void Dispose()
		{
			// Clean up
			_MasterCondition = null;

			if (_SlaveSubroutine != null)
			{
				_SlaveSubroutine.Dispose();
				_SlaveSubroutine = null;
			}

			_CurrentState = WhileNodeState.Disposed;
		}

		public void Reset()
		{
			_SlaveSubroutine.Reset();
			_CurrentState = WhileNodeState.Running;
		}
	}

	/// <summary>
	/// A state node is a strongly typed coroutine node that doesn't terminate
	/// It can report a 'state' through the return type, but it can be executed forever.
	/// It is always Running! For instance, behaviour graph nodes are States!
	/// </summary>
	public abstract class State
		: ICoroutine
	{
		public bool IsRunning
		{
			get
			{
				// State is always running
				return true;
			}
		}

		public abstract void Update();

		public abstract void Dispose();

		public abstract void Reset();

	}

	/// <summary>
	/// A state node is a strongly typed coroutine node that doesn't terminate
	/// It can report a 'state' through the return type, but it can be executed forever.
	/// It is always Running! For instance, behaviour graph nodes are States!
	/// </summary>
	public abstract class State<T>
		: ICoroutine<T>
	{
		public bool IsRunning
		{
			get
			{
				// Behaviour node is always running
				return true;
			}
		}

		public abstract bool HasValue { get; }

		public abstract T Value { get; }

		public abstract void Update();

		public abstract void Dispose();

		public abstract void Reset();

	}

	/// <summary>
	/// Wrapper node that makes a basic coroutine node look like a state (i.e. be always running)
	/// but exposes that running/complete state as a boolean value, so that nodes that expect to
	/// work with State nodes can work with basic coroutine nodes. So for instance, you can make
	/// A bunch of slave nodes execute as long as a 'master' node is itself executing (the While<T> node)
	/// </summary>
	[System.Serializable]
	public class TrueWhileRunning
		: State<bool>
	{
		ICoroutine _Subroutine;

		public override bool HasValue
		{
			get
			{
				return true;
			}
		}

		public override bool Value
		{
			get
			{
				return _Subroutine.IsRunning;
			}
		}

		public TrueWhileRunning(ICoroutine subroutine)
		{
			_Subroutine = subroutine;
		}

		public override void Dispose()
		{
			_Subroutine.Dispose();
			_Subroutine = null;
		}

		public override void Reset()
		{
			_Subroutine.Reset();
		}

		public override void Update()
		{
			_Subroutine.Update();
		}
	}


	/// <summary>
	/// Executes the slave node for as long as the master condition, applied to the master node's return value is true,
	/// As soon as the master node completes, stop executing the slave node
	/// </summary>
	[System.Serializable]
	public class While<T>
		: ICoroutine<T>
	{
		While _WhileNode;
		State<T> _MasterState;

		public While(State<T> masterNode, ICoroutine slaveNode, System.Func<T, bool> masterCondition)
		{
			// Create master node, that controls execution
			_MasterState = masterNode;

			// Create our slave node
			_WhileNode = new While(slaveNode, () => !_MasterState.HasValue || masterCondition(_MasterState.Value));
		}

		public bool IsRunning
		{
			get
			{
				return _WhileNode.IsRunning;
			}
		}

		public bool HasValue
		{
			get
			{
				return _MasterState.HasValue;
			}
		}

		public T Value
		{
			get
			{
				return _MasterState.Value;
			}
		}

		public void Update()
		{
			_MasterState.Update();
			_WhileNode.Update();
		}

		public void Dispose()
		{
			// Clean up
			if (_MasterState != null)
			{
				_MasterState.Dispose();
				_MasterState = null;
			}

			if (_WhileNode != null)
			{
				_WhileNode.Dispose();
				_WhileNode = null;
			}
		}

		public void Reset()
		{
			_MasterState.Reset();
			_WhileNode.Reset();
		}
	}


	/// <summary>
	/// The state coroutine node is the simplest implementation of the state node
	/// around a coroutine. If the coroutine completes, it gets reset and reexcuted
	/// on the very same frame.
	/// </summary>
	[System.Serializable]
	public class StateCoroutine<T>
		: State<T>
	{
		// Extend by composition
		ICoroutine<T> _Subroutine;

		public override bool HasValue
		{
			get
			{
				return _Subroutine.HasValue;
			}
		}

		public override T Value
		{
			get
			{
				return _Subroutine.Value;
			}
		}

		public StateCoroutine(IEnumerable<Instruction<T>> stateSource)
		{
			_Subroutine = new Coroutine<T>(stateSource);
		}

		public override void Update()
		{
			Debug.Assert(_Subroutine.IsRunning);

			// Update the subroutine once
			_Subroutine.Update();

			// If it has reached the end of its code block, we need to start over
			// And we want to start over this frame, not next
			// However we don't want to run into an infinite loop!
			if (_Subroutine.IsComplete())
			{
				_Subroutine.Reset();
				_Subroutine.Update();

				// At this point, the subroutine must be running, otherwise it means we have an infinite loop
				if (_Subroutine.IsComplete())
				{
					throw new System.Exception("State - the subroutine is in an infinite loop (it didn't hit any yield statement), aborting");
				}
				// Else the node is now running, we have successfully restarted it!
			}
			// Else the node is still running, great!
		}

		public override void Dispose()
		{
			_Subroutine.Dispose();
			_Subroutine = null;
		}

		public override void Reset()
		{
			// Pass it on!
			_Subroutine.Reset();
		}
	}

}
