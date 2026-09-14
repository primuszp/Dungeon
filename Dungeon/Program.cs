using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace Dungeon;

internal static class Program
{
    private static void Main()
    {
        GameWindowSettings gameSettings = new() { UpdateFrequency = 120 };
        NativeWindowSettings windowSettings = new()
        {
            ClientSize = new Vector2i(1280, 720),
            Title = "THE SUNKEN KEEP  |  WASD · egér · Shift futás · F11 teljes képernyő",
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible,
            NumberOfSamples = 4
        };
        using DungeonGame game = new(gameSettings, windowSettings);
        game.Run();
    }
}
