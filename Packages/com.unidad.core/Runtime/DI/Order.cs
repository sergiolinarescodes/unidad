namespace Unidad.Core.DI
{
    /// <summary>Ranks of single registrations: the highest wins a contract, ties go to the later registration.</summary>
    public static class Order
    {
        /// <summary>A stub's rank; any real registration beats it.</summary>
        public const int Stub = int.MinValue;

        public const int Default = 0;
    }
}
