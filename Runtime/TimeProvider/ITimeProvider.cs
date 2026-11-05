using System;

namespace Damdor.Temporio
{
    /// <summary>
    /// Defines a source of current date and time for timer and scheduling components.
    /// </summary>
    public interface ITimeProvider
    {
        public DateTime Now { get; }
    }
}