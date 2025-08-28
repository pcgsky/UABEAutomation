using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.Core.Conditions;

public static class FlaUIHelpers
{
    public enum ButtonType { Ok, Load, Save, Info, Plugin }
    public static Window GetWindowByName(Application app, AutomationBase automation,
                                        string windowName, int timeoutSecs = 5)
    {
        return Retry.WhileNull( // Retry until the window is found or timeout occurs
            () => app?
                .GetAllTopLevelWindows(automation) // Get all top-level windows
                .FirstOrDefault(w => w.Name == windowName), // Finds "windowName" window from the array
            timeout: TimeSpan.FromSeconds(timeoutSecs)
        ).Result ?? throw new Exception($"'{windowName}' Window  not found");
    }

    public static AutomationElement GetWindowChild(Window window, Func<ConditionFactory, ConditionBase> conditionFunc, int timeoutSecs = 5)
    {
        return Retry.WhileNull(
            () => window?.FindFirstDescendant(conditionFunc),
            timeout: TimeSpan.FromSeconds(timeoutSecs)
        ).Result ?? throw new Exception("Element not found");
    }
    
    public static void ClickButton(AutomationElement window, ButtonType btnType)
    {
        string automationId = btnType switch
        {
            ButtonType.Ok => "btnOk",
            ButtonType.Load => "btnLoad",
            ButtonType.Save => "btnSave",
            ButtonType.Info => "btnInfo",
            ButtonType.Plugin => "btnPlugin",
            _ => throw new ArgumentOutOfRangeException(nameof(btnType), btnType, null)
        };

        var button = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)).AsButton() ?? throw new Exception($"{btnType} button not found");
        button.Click();
        
    }
}