using UnityEngine.InputSystem;

public partial class LeonInputBridge
{
    internal string ReadPromptBinding(string actionName, bool controlPath = false)
    {
        InputAction action = interactAction;
        if (actionName == "Shoot")
        {
            action = shootAction;
        }
        else if (actionName == "Parry")
        {
            action = parryAction;
        }
        else if (actionName == "EscapePress")
        {
            action = escapePressAction;
        }
        else if (actionName == "KnifeEscape")
        {
            action = knifeEscapeAction;
        }
        if (action == null)
        {
            return string.Empty;
        }
        for (int index = 0; index < action.bindings.Count; index++)
        {
            InputBinding binding = action.bindings[index];
            string path = binding.effectivePath;
            if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(path))
            {
                continue;
            }
            if (path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>"))
            {
                if (controlPath)
                {
                    return path;
                }
                return action.GetBindingDisplayString(index, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            }
        }
        return action.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions);
    }
}
