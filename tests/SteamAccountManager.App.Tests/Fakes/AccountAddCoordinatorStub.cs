using System;
using System.Threading;
using System.Threading.Tasks;
using SteamAccountManager.App.Models;
using SteamAccountManager.App.Services;

namespace SteamAccountManager.App.Tests.Fakes;

public sealed class AccountAddCoordinatorStub : IAccountAddCoordinator
{
    public int Calls { get; private set; }
    public AccountListItem? Result { get; set; }

    public Task<AccountListItem?> BeginAddAndWaitAsync(TimeSpan pollInterval, TimeSpan timeout, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(Result);
    }
}
