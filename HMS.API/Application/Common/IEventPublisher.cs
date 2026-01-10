using System.Threading.Tasks;

namespace HMS.API.Application.Common
{
    public interface IEventPublisher
    {
        Task PublishAsync(object @event);
    }
}