namespace Unidad.Core.Patterns.CommandQueue
{
    public interface ICommand
    {
        string Id { get; }
        CommandStatus Execute(ICommandContext context, float deltaTime);
        void Cancel();
    }
}
