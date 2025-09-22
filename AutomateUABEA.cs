using WindowsInput;
using WindowsInput.Native;
using FlaUI.UIA3;
using FlaUI.Core;
using FlaUI.Core.Input;
using FlaUI.Core.AutomationElements;
using System.Text.Json;
using FlaUI.Core.Tools;
class AutomateUABEA #aaa
{
    private readonly InputSimulator _sim;
    private readonly string _exePath;
    private Application? _app;
    private UIA3Automation? _automation;

    public AutomateUABEA()
    {
        _sim = new InputSimulator();
        string configPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "config", "config.json");
        _exePath = LoadExePath(configPath);
        _app = null;
        _automation = null;

    }

    /// <summary>
    /// Loads the UABEA executable path from a JSON config file.
    /// </summary>
    private string LoadExePath(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file not found: {path}");

        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("exePath", out var exePathElement))
            throw new InvalidOperationException("Missing 'exePath' in config.");

        string exePath = exePathElement.GetString();
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            throw new FileNotFoundException($"Executable not found: {exePath}");

        return exePath;
    }


    // Shortcut methods
    private void OpenFileDialogShortcut() { _sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_O); }
    private void SaveAssetShortcut() { _sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_S); }
    private void GoToAssetShortcut() { _sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_G); }

    private void LaunchUABEAvalonia()
    {
        Console.WriteLine("Launching UABE Avalonia...");
        Application.Launch(_exePath);
        _app = Retry.WhileNull(() => Application.Attach("UABEAvalonia"), TimeSpan.FromSeconds(10)).Result;
        _automation = new UIA3Automation();
    }
    /// <summary>
    /// Loads a Unity3d asset file into UABEA 
    /// </summary>
    /// <param name="assetPath">Path to the Unity3d asset file</param>
    private void LoadAssetFile(string assetPath, int timeoutSecs = 5)
    {
        var uabeaWindow = FlaUIHelpers.GetWindowByName(_app, _automation, "UABEA", timeoutSecs); // Get UABEA window
        OpenFileDialogShortcut();//Press keyboard shortcut to open file dialog
        var fileDialog = Retry.WhileNull( // Grabs the file dialog window
            () => uabeaWindow
                .FindFirstDescendant(w => w.ByName("Open assets or bundle file")), 
                timeout: TimeSpan.FromSeconds(timeoutSecs)
        ).Result ?? throw new Exception("File dialog not found");

        EnterFilePathInDialog(assetPath);
        Console.WriteLine("Asset file Loaded");
    }
    /// <summary>
    /// Enters the file path into file explorer dialog and presses Enter
    /// </summary>
    private void EnterFilePathInDialog(string filePath, int delay = 200)
    {
        _sim.Keyboard.TextEntry(filePath);// Types file path
        Thread.Sleep(delay);
        _sim.Keyboard.KeyPress(VirtualKeyCode.RETURN);
        Thread.Sleep(delay);
    }
    
    /// <summary>
    /// Closes the Message Box window by clicking OK button
    /// The Message Box appears after trying to save the Assets Info window
    /// </summary>
    private void CloseMessageBoxWindow()
    {
        var allWindows = _app?.GetAllTopLevelWindows(_automation);
        var assetsInfoWindow = allWindows?.FirstOrDefault(w => w.ClassName == "InfoWindow") ?? throw new Exception("Assets Info window not found");
        var messageBoxWindow = assetsInfoWindow?.FindFirstDescendant(cf => cf.ByName("Message Box")) ?? throw new Exception("Message Box not found");

        var button = messageBoxWindow.FindFirstDescendant(cf => cf.ByAutomationId("btn1")).AsButton() ?? throw new Exception($"btn1 button not found");
        button.Click();
      
    }
    /// <summary>
    /// After UABEA is open and asset is loaded, this opens the Asset Info window by clicking the Info button
    /// </summary>
    private void OpenInfoWindow()
    {
        var allWindows = _app?.GetAllTopLevelWindows(_automation);
        var uabeaWindow = allWindows?.FirstOrDefault(w => w.Name == "UABEA") ?? throw new Exception("UABEA window not found");
        FlaUIHelpers.ClickButton(uabeaWindow, FlaUIHelpers.ButtonType.Info);
    }
    
    /// <summary>
    /// After Asset Info window is open, this searches for an asset by fileId and pathId
    /// </summary>
    private void SearchAsset(int fileId, int pathId, int timeoutSecs = 5)
    {
        GoToAssetShortcut();
        // finds Assets Info window 
        var assetsInfoWindow = FlaUIHelpers.GetWindowByName(_app, _automation, "Assets Info", timeoutSecs);
        // finds "Go to asset" window
        var goToAssetWindow = FlaUIHelpers.GetWindowChild(assetsInfoWindow, cf => cf.ByName("Go to asset"), timeoutSecs);


        // finds fileId Dropdown
        var comboBoxElement = goToAssetWindow.FindFirstDescendant(cf => cf.ByClassName("ComboBox"))?.AsComboBox() ?? throw new Exception("Dropdown not found");
        var toggleButton = comboBoxElement.FindFirstDescendant(cf => cf.ByClassName("ToggleButton")) ?? throw new Exception("ToggleButton not found");
        // Click to open dropdown
        var rect = toggleButton.BoundingRectangle;
        Mouse.MoveTo(rect.Center());
        Mouse.Click(FlaUI.Core.Input.MouseButton.Left);
        Thread.Sleep(100);

        // Now select by index
        var comboBoxEl = comboBoxElement.AsComboBox();
        if (comboBoxEl.Items.Length > fileId)
            comboBoxEl.Select(fileId); // select 4th item
        else
            throw new Exception("Not enough items in dropdown");
        Mouse.Click(FlaUI.Core.Input.MouseButton.Left);

        // finds and types into PathId TextBox 
        var textBox = (goToAssetWindow.FindFirstDescendant(cf => cf.ByAutomationId("boxPathId"))?.AsTextBox()) ?? throw new Exception("TextBox not found");
        textBox.Text = pathId.ToString();  // types into it

        FlaUIHelpers.ClickButton(goToAssetWindow, FlaUIHelpers.ButtonType.Ok);
    }

    /// <summary>
    /// After Asset Info window is open, this opens the Edit Texture plugin, loads a texture, and saves it
    /// Expect UABEA to freeze for a minute while the texture is being processed
    /// </summary>
    private void LoadTexture(string texturePath, int timeoutSecs = 5)
    {
        // Gets Assets Info window 
        var assetsInfoWindow = FlaUIHelpers.GetWindowByName(_app, _automation, "Assets Info", timeoutSecs);
        FlaUIHelpers.ClickButton(assetsInfoWindow, FlaUIHelpers.ButtonType.Plugin);

        var pluginsWindow = assetsInfoWindow?.FindFirstDescendant(cf => cf.ByName("Plugins")) ?? throw new Exception("Plugins not found");
        var listBox = pluginsWindow.FindFirstDescendant(cf => cf.ByClassName("ListBox"))?.AsListBox() ?? throw new Exception("ListBox not found");
        var items = listBox.Items;
        var editTexture = items.FirstOrDefault(i => i.Name.Equals("Edit Texture", StringComparison.OrdinalIgnoreCase)) ?? throw new Exception("Edit Texture not found");
        editTexture.Click();

        FlaUIHelpers.ClickButton(pluginsWindow, FlaUIHelpers.ButtonType.Ok);
        var textureEditWindow = assetsInfoWindow?.FindFirstDescendant(cf => cf.ByName("Texture Edit")) ?? throw new Exception("Texture Edit not found");

        FlaUIHelpers.ClickButton(textureEditWindow, FlaUIHelpers.ButtonType.Load);
        EnterFilePathInDialog(texturePath);

        FlaUIHelpers.ClickButton(textureEditWindow, FlaUIHelpers.ButtonType.Save);
        Wait.UntilResponsive(assetsInfoWindow, timeout: TimeSpan.FromMinutes(10));// The whole program freezes here while processing the texture
        Console.WriteLine("Texture loaded");
    }
    private void SaveAsset(int timeoutSecs = 5)
    {
        var assetsInfoWindow = FlaUIHelpers.GetWindowByName(_app, _automation, "Assets Info", timeoutSecs);
        var uabeaWindow = FlaUIHelpers.GetWindowByName(_app, _automation, "UABEA", timeoutSecs);

        SaveAssetShortcut();
        CloseMessageBoxWindow();
        assetsInfoWindow.Close();
        SaveAssetShortcut();
        uabeaWindow.Close();
    }
    public void Dispose()
    {
        _automation?.Dispose();
        _app?.Close();
    }

    public void AutomateTextureReplacement(string assetPath, string texturePath, int fileId, int pathId)
    {
        LaunchUABEAvalonia();
        LoadAssetFile(assetPath);
        OpenInfoWindow();
        SearchAsset(fileId, pathId);
        LoadTexture(texturePath);
        SaveAsset();
    }
    static void Main(string[] args)
    {
       
        if (args.Length < 4)
        {
            Console.WriteLine("Usage: UABEAHelper.exe <assetPath> <texturePath> <fileId> <pathId>");
            return;
        }

        string assetPath = args[0];
        string texturePath = args[1];
        if (!int.TryParse(args[2], out int fileId) || !int.TryParse(args[3], out int pathId))
        {
            Console.WriteLine("fileId and pathId must be integers.");
            return;
        }

        AutomateUABEA automator = new AutomateUABEA();
        automator.AutomateTextureReplacement(assetPath, texturePath, fileId, pathId);


    }
}

