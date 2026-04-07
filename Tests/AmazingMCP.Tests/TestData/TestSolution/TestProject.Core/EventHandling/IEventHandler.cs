namespace TestProject.Core.EventHandling;

public interface IEventHandler<TEvent, TResult>
{
    TResult Handle(TEvent evt);
}
