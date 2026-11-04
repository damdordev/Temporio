using System;

namespace Damdor.Temporio
{
    /// <summary>
    /// Represents a method that handles exceptions raised during timer operations.
    /// </summary>
    /// <param name="debugName">The debug name associated with the timer that produced the exception.</param>
    /// <param name="exception">The exception that occurred during timer execution or management.</param>
    public delegate void TimerExceptionHandler(string debugName, Exception exception);
}