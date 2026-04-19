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
        None
    }

    public static class Constants
    {
        public static string ExecutionPath { get; set; } = AppContext.BaseDirectory;
    }

    public interface IMonitor
    {
        void Log(string message, LogLevel level = LogLevel.Debug);
    }

    public interface IModManifest
    {
        string UniqueID { get; }
    }

    public interface IConsoleCommandHelper
    {
        void Add(string name, string documentation, Action<string, string[]> callback);
    }

    public interface IGameLoopEvents
    {
        event EventHandler<Events.GameLaunchedEventArgs> GameLaunched;
    }

    public interface IInputEvents
    {
        event EventHandler<Events.ButtonPressedEventArgs> ButtonPressed;
    }

    public interface IEventHelper
    {
        IGameLoopEvents GameLoop { get; }
        IInputEvents Input { get; }
    }

    public interface IModHelper
    {
        string DirectoryPath { get; }
        IEventHelper Events { get; }
        IConsoleCommandHelper ConsoleCommands { get; }
        T ReadConfig<T>() where T : new();
    }

    public abstract class Mod
    {
        public IModHelper Helper { get; set; } = null!;
        public IMonitor Monitor { get; set; } = null!;
        public IModManifest ModManifest { get; set; } = null!;

        public abstract void Entry(IModHelper helper);
    }
}

namespace StardewModdingAPI.Events
{
    public sealed class GameLaunchedEventArgs : EventArgs
    {
    }

    public sealed class ButtonPressedEventArgs : EventArgs
    {
        public ButtonPressedEventArgs(StardewModdingAPI.SButton button)
        {
            Button = button;
        }

        public StardewModdingAPI.SButton Button { get; }
    }
}
