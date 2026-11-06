namespace Damdor.Temporio
{
    internal class InternalTimerGroup
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public bool IsPaused { get; set; }

        internal void Reset()
        {
            Id = Id;
            Name = null;
            IsPaused = false;
        }
    }
}
