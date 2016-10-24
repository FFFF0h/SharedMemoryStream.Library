#if NET35 || NET40

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Identities the async state machine type for this method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    [Serializable]
    public sealed class AsyncStateMachineAttribute : StateMachineAttribute
    {
        /// <summary>
        /// Initializes the attribute.
        /// </summary>
        /// <param name="stateMachineType">The type that implements the state machine.</param>
        public AsyncStateMachineAttribute(Type stateMachineType)
            : base(stateMachineType)
        {
        }
    }
}

#endif