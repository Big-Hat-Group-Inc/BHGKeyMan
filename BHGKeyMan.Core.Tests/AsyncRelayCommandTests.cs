using BHGKeyMan.App;

namespace BHGKeyMan.Core.Tests;

public class AsyncRelayCommandTests
{
    [Fact]
    public async Task Execute_SetsRunningStateAndInvokesCallback()
    {
        var callbackCount = 0;
        var gate = new TaskCompletionSource();
        var completion = new TaskCompletionSource();
        var command = new AsyncRelayCommand(
            async () =>
            {
                completion.SetResult();
                await gate.Task;
            },
            executionStateChanged: () => callbackCount++);

        command.Execute(null);
        await completion.Task;

        Assert.True(command.IsRunning);
        Assert.False(command.CanExecute(null));

        gate.SetResult();
        await Task.Delay(50);

        Assert.False(command.IsRunning);
        Assert.True(command.CanExecute(null));
        Assert.True(callbackCount >= 2);
    }
}
