namespace Unidad.Core.Content
{
    /// <summary>A cross-reference check run once after the container builds; added with <c>AddMany&lt;IContentCheck&gt;</c>.</summary>
    public interface IContentCheck
    {
        string Name { get; }

        void Check(ContentReport report);
    }
}
