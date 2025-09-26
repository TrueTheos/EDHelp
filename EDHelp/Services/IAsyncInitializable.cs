using System.Threading.Tasks;

namespace EDHelp.Services;

public interface IAsyncInitializable
{
    public Task InitializeAsync();
}