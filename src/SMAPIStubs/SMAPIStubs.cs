namespace StardewModdingAPI
{
    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warn,
        Error,
        Alert
    }

    public enum SButton
    {
        None = 0
    }

    public static class Constants
    {
        public static string GamePath { get; set; } = AppContext.BaseDirectory;
    }

    public interface IMonitor
    {
        void Log(string message, LogLevel level = LogLevel.Trace);
    }

    public interface IModLinked
    {
        string ModID { get; }
    }

    public interface ICommandHelper : IModLinked
    {
        ICommandHelper Add(string name, string documentation, Action<string, string[]> callback);
    }

    public interface IModInfo
    {
    }

    public interface IModHelper
    {
        string DirectoryPath { get; }
        Events.IModEvents Events { get; }
        ICommandHelper ConsoleCommands { get; }
        TConfig ReadConfig<TConfig>() where TConfig : class, new();
    }

    public interface IMod
    {
        IModHelper Helper { get; }
        IMonitor Monitor { get; }
        IManifest ModManifest { get; }
        void Entry(IModHelper helper);
        object? GetApi();
        object? GetApi(IModInfo mod);
    }

    public abstract class Mod : IMod, IDisposable
    {
        public IModHelper Helper { get; internal set; } = null!;
        public IMonitor Monitor { get; internal set; } = null!;
        public IManifest ModManifest { get; internal set; } = null!;

        public abstract void Entry(IModHelper helper);

        public virtual object? GetApi() => null;
        public virtual object? GetApi(IModInfo mod) => null;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) { }
    }
}

namespace StardewModdingAPI.Events
{
    public interface IModEvents
    {
        IGameLoopEvents GameLoop { get; }
        IInputEvents Input { get; }
    }

    public interface IGameLoopEvents
    {
        event EventHandler<GameLaunchedEventArgs> GameLaunched;
    }

    public interface IInputEvents
    {
        event EventHandler<ButtonPressedEventArgs> ButtonPressed;
    }

    public class GameLaunchedEventArgs : EventArgs
    {
    }

    public class ButtonPressedEventArgs : EventArgs
    {
        public ButtonPressedEventArgs(StardewModdingAPI.SButton button)
        {
            Button = button;
        }

        public StardewModdingAPI.SButton Button { get; }
    }
}
